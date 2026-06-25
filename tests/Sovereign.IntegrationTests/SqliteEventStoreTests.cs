using Sovereign.Contracts.Ipc;
using Sovereign.Storage;
using Xunit;

namespace Sovereign.IntegrationTests;

/// <summary>
/// Integration tests for the SQLite event store: append/query/count behavior and persistence
/// across a simulated service restart (agent_start.md section 12.2).
/// </summary>
public sealed class SqliteEventStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovereign-test-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task AppendThenQuery_ReturnsEventsInOrder()
    {
        await using var store = new SqliteEventStore(this._dbPath);
        await store.InitializeAsync(CancellationToken.None);

        long id1 = await store.AppendAsync("cat.a", "first", CancellationToken.None);
        long id2 = await store.AppendAsync("cat.b", "second", CancellationToken.None);

        Assert.True(id2 > id1);

        IReadOnlyList<EventRecord> events = await store.QueryAsync(100, afterId: null, CancellationToken.None);

        Assert.Equal(2, events.Count);
        Assert.Equal("first", events[0].Message);
        Assert.Equal("second", events[1].Message);
        Assert.Equal(2, await store.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Query_WithAfterId_ReturnsOnlyNewerEvents()
    {
        await using var store = new SqliteEventStore(this._dbPath);
        await store.InitializeAsync(CancellationToken.None);

        long id1 = await store.AppendAsync("cat", "one", CancellationToken.None);
        await store.AppendAsync("cat", "two", CancellationToken.None);

        IReadOnlyList<EventRecord> events = await store.QueryAsync(100, afterId: id1, CancellationToken.None);

        EventRecord only = Assert.Single(events);
        Assert.Equal("two", only.Message);
    }

    [Fact]
    public async Task Events_PersistAcrossReopen_SimulatingServiceRestart()
    {
        await using (var store = new SqliteEventStore(this._dbPath))
        {
            await store.InitializeAsync(CancellationToken.None);
            await store.AppendAsync("service.start", "started", CancellationToken.None);
        }

        await using (var reopened = new SqliteEventStore(this._dbPath))
        {
            await reopened.InitializeAsync(CancellationToken.None);

            Assert.Equal(1, await reopened.CountAsync(CancellationToken.None));
            IReadOnlyList<EventRecord> events = await reopened.QueryAsync(100, afterId: null, CancellationToken.None);
            Assert.Equal("started", Assert.Single(events).Message);
        }
    }

    public void Dispose()
    {
        TryDelete(this._dbPath);
        TryDelete(this._dbPath + "-wal");
        TryDelete(this._dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of temp files.
        }
    }
}
