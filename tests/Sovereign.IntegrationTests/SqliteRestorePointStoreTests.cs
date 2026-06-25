using Sovereign.Storage;
using Xunit;

namespace Sovereign.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="SqliteRestorePointStore"/>: restore points round-trip and
/// survive a simulated service restart (a fresh store instance over the same file).
/// </summary>
public sealed class SqliteRestorePointStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovereign-rp-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SaveThenGetLatest_ReturnsMostRecentForPolicy()
    {
        await using var store = new SqliteRestorePointStore(this._dbPath);
        await store.InitializeAsync(CancellationToken.None);

        await store.SaveAsync("p1", "corr-1", "[1]", CancellationToken.None);
        await store.SaveAsync("p1", "corr-2", "[2]", CancellationToken.None);
        await store.SaveAsync("p2", "corr-3", "[3]", CancellationToken.None);

        RestorePoint? latestP1 = await store.GetLatestAsync("p1", CancellationToken.None);
        RestorePoint? latestP2 = await store.GetLatestAsync("p2", CancellationToken.None);

        Assert.NotNull(latestP1);
        Assert.Equal("corr-2", latestP1!.CorrelationId);
        Assert.Equal("[2]", latestP1.PayloadJson);
        Assert.NotNull(latestP2);
        Assert.Equal("[3]", latestP2!.PayloadJson);
    }

    [Fact]
    public async Task GetLatest_ForUnknownPolicy_ReturnsNull()
    {
        await using var store = new SqliteRestorePointStore(this._dbPath);
        await store.InitializeAsync(CancellationToken.None);

        Assert.Null(await store.GetLatestAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task RestorePoints_PersistAcrossRestart()
    {
        await using (var first = new SqliteRestorePointStore(this._dbPath))
        {
            await first.InitializeAsync(CancellationToken.None);
            await first.SaveAsync("p", "corr", "[\"captured\"]", CancellationToken.None);
        }

        await using var second = new SqliteRestorePointStore(this._dbPath);
        await second.InitializeAsync(CancellationToken.None);
        RestorePoint? latest = await second.GetLatestAsync("p", CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal("[\"captured\"]", latest!.PayloadJson);
    }

    public void Dispose()
    {
        foreach (string path in new[] { this._dbPath, this._dbPath + "-wal", this._dbPath + "-shm" })
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
            }
        }
    }
}
