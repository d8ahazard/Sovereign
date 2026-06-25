using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Storage;

/// <summary>
/// A SQLite-backed implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// Uses a single guarded connection with WAL journaling. The schema is versioned via
/// <c>PRAGMA user_version</c> and migrated forward on initialization. Failures throw rather than
/// degrade silently (agent_start.md section 2.2/2.3).
/// </remarks>
public sealed class SqliteEventStore : IEventStore, IAsyncDisposable
{
    /// <summary>The current schema version produced by the migrations in this build.</summary>
    public const int CurrentSchemaVersion = 1;

    private const int MaxQueryLimit = 1000;

    // Constant SQL only (no interpolation of runtime values); parameters are used for all data.
    // The literal must match CurrentSchemaVersion (asserted by a unit test).
    private const string SetUserVersionSql = "PRAGMA user_version=1;";

    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    /// <summary>
    /// Creates a store backed by the given database file path.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file. Its directory is created if missing.</param>
    public SqliteEventStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        this._connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = new SqliteConnection(this._connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (SqliteCommand pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

            this._connection = connection;
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<long> AppendAsync(string category, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection connection = this.RequireConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO events (timestamp_utc, category, message) VALUES ($ts, $cat, $msg); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$cat", category);
            command.Parameters.AddWithValue("$msg", message);

            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<EventRecord>> QueryAsync(int limit, long? afterId, CancellationToken cancellationToken)
    {
        int clamped = Math.Clamp(limit, 1, MaxQueryLimit);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection connection = this.RequireConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT id, timestamp_utc, category, message FROM events WHERE id > $after ORDER BY id ASC LIMIT $limit;";
            command.Parameters.AddWithValue("$after", afterId ?? 0L);
            command.Parameters.AddWithValue("$limit", clamped);

            var results = new List<EventRecord>();
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                long id = reader.GetInt64(0);
                DateTimeOffset ts = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                string category = reader.GetString(2);
                string message = reader.GetString(3);
                results.Add(new EventRecord(id, ts, category, message));
            }

            return results;
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection connection = this.RequireConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM events;";
            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            this._gate.Release();
        }
    }

    private SqliteConnection RequireConnection() =>
        this._connection ?? throw new InvalidOperationException("The event store has not been initialized. Call InitializeAsync first.");

    private static async ValueTask MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        int version = await GetUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);

        if (version < 1)
        {
            await using (SqliteCommand create = connection.CreateCommand())
            {
                create.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS events (
                        id            INTEGER PRIMARY KEY AUTOINCREMENT,
                        timestamp_utc TEXT NOT NULL,
                        category      TEXT NOT NULL,
                        message       TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_events_category ON events (category);
                    """;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (SqliteCommand setVersion = connection.CreateCommand())
            {
                setVersion.CommandText = SetUserVersionSql;
                await setVersion.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<int> GetUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._connection is not null)
        {
            await this._connection.DisposeAsync().ConfigureAwait(false);
            this._connection = null;
        }

        this._gate.Dispose();
    }
}
