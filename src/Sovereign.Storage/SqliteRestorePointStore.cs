using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Sovereign.Storage;

/// <summary>
/// A SQLite-backed implementation of <see cref="IRestorePointStore"/>.
/// </summary>
/// <remarks>
/// Shares the database file with <see cref="SqliteEventStore"/> but owns its own table and
/// connection. The table is created idempotently on initialization, so initialization order
/// relative to the event store does not matter. WAL journaling allows concurrent readers/writers.
/// </remarks>
public sealed class SqliteRestorePointStore : IRestorePointStore, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    /// <summary>
    /// Creates a store backed by the given database file path.
    /// </summary>
    /// <param name="databasePath">Full path to the SQLite database file. Its directory is created if missing.</param>
    public SqliteRestorePointStore(string databasePath)
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

            await using (SqliteCommand create = connection.CreateCommand())
            {
                create.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS restore_points (
                        id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        policy_id      TEXT NOT NULL,
                        correlation_id TEXT NOT NULL,
                        created_utc    TEXT NOT NULL,
                        payload_json   TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_restore_points_policy ON restore_points (policy_id, id);
                    """;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            this._connection = connection;
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<long> SaveAsync(string policyId, string correlationId, string payloadJson, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(payloadJson);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection connection = this.RequireConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO restore_points (policy_id, correlation_id, created_utc, payload_json) VALUES ($pid, $cid, $ts, $payload); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$pid", policyId);
            command.Parameters.AddWithValue("$cid", correlationId);
            command.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$payload", payloadJson);

            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<RestorePoint?> GetLatestAsync(string policyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection connection = this.RequireConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT id, policy_id, correlation_id, created_utc, payload_json FROM restore_points WHERE policy_id = $pid ORDER BY id DESC LIMIT 1;";
            command.Parameters.AddWithValue("$pid", policyId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            long id = reader.GetInt64(0);
            string pid = reader.GetString(1);
            string cid = reader.GetString(2);
            DateTimeOffset created = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            string payload = reader.GetString(4);
            return new RestorePoint(id, pid, cid, created, payload);
        }
        finally
        {
            this._gate.Release();
        }
    }

    private SqliteConnection RequireConnection() =>
        this._connection ?? throw new InvalidOperationException("The restore-point store has not been initialized. Call InitializeAsync first.");

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
