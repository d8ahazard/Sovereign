using Sovereign.Contracts;
using Sovereign.Policy;
using Xunit;

namespace Sovereign.UnitTests;

/// <summary>
/// Unit tests for the declarative <see cref="PolicyEngine"/> proving the Milestone 2 gate
/// (agent_start.md section 8 and Milestone 2): repeated apply is idempotent, partial failure rolls
/// back safely, and <see cref="PolicyResultState.Unknown"/> / <see cref="PolicyResultState.Unsupported"/>
/// are never compliant.
/// </summary>
public sealed class PolicyEngineTests
{
    private static PolicyEngine CreateEngine(ISettingProvider provider, out RecordingEventStore events)
    {
        events = new RecordingEventStore();
        return new PolicyEngine(provider, new InMemoryRestorePointStore(), events);
    }

    [Fact]
    public async Task Detect_ReportsNonCompliant_ThenCompliant_AfterApply()
    {
        var provider = new InMemorySettingProvider();
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("k", "1")]);

        Assert.Equal(PolicyResultState.NonCompliant, await engine.DetectAsync(policy, default));

        PolicyExecutionReport report = await engine.ApplyAsync(policy, default);
        Assert.Equal(PolicyResultState.Applied, report.State);
        Assert.Equal(PolicyResultState.Compliant, await engine.DetectAsync(policy, default));
    }

    [Fact]
    public async Task Detect_Unsupported_IsNeverCompliant()
    {
        PolicyEngine engine = CreateEngine(new InMemorySettingProvider(), out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("k", "1")], supported: false);

        PolicyResultState state = await engine.DetectAsync(policy, default);

        Assert.Equal(PolicyResultState.Unsupported, state);
        Assert.NotEqual(PolicyResultState.Compliant, state);
    }

    [Fact]
    public async Task Detect_Unknown_OnProviderReadFailure_IsNeverCompliant()
    {
        var provider = new FaultySettingProvider();
        provider.FailGetKeys.Add("k");
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("k", "1")]);

        PolicyResultState state = await engine.DetectAsync(policy, default);

        Assert.Equal(PolicyResultState.Unknown, state);
        Assert.NotEqual(PolicyResultState.Compliant, state);
    }

    [Fact]
    public async Task Apply_IsIdempotent_SecondApplyIsNoOp()
    {
        var provider = new InMemorySettingProvider();
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("a", "1"), TestPolicy.Present("b", "2")]);

        PolicyExecutionReport first = await engine.ApplyAsync(policy, default);
        Assert.Equal(PolicyResultState.Applied, first.State);
        Assert.Equal(2, first.Plan.Changes.Count);

        PolicyExecutionReport second = await engine.ApplyAsync(policy, default);
        Assert.Equal(PolicyResultState.Compliant, second.State);
        Assert.True(second.Plan.IsNoOp);
    }

    [Fact]
    public async Task Apply_PartialFailure_RollsBackAllChanges_AndIsNeverCompliant()
    {
        var provider = new FaultySettingProvider();
        provider.FailSetPresentKeys.Add("b"); // the second write fails
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("a", "1"), TestPolicy.Present("b", "2")]);

        PolicyExecutionReport report = await engine.ApplyAsync(policy, default);

        Assert.Equal(PolicyResultState.VerificationFailed, report.State);
        Assert.NotEqual(PolicyResultState.Compliant, report.State);
        Assert.NotNull(report.FailureDetail);

        // The first change must have been rolled back to its original (absent) value.
        Assert.False((await provider.GetAsync("a", default)).Exists);
        Assert.False((await provider.GetAsync("b", default)).Exists);
    }

    [Fact]
    public async Task Apply_VerifyMismatch_ReportsVerificationFailed_AndRollsBack()
    {
        var provider = new FaultySettingProvider();
        provider.IgnoreSetKeys.Add("k"); // write silently does nothing, so read-back fails verification
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("k", "1")]);

        PolicyExecutionReport report = await engine.ApplyAsync(policy, default);

        Assert.Equal(PolicyResultState.VerificationFailed, report.State);
        Assert.False((await provider.GetAsync("k", default)).Exists);
    }

    [Fact]
    public async Task Apply_WhenRollbackAlsoFails_ReportsRollbackFailed()
    {
        var provider = new FaultySettingProvider();
        provider.FailSetPresentKeys.Add("b"); // second write fails, triggering rollback
        provider.FailClearKeys.Add("a");       // restoring the first change to absent also fails
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("a", "1"), TestPolicy.Present("b", "2")]);

        PolicyExecutionReport report = await engine.ApplyAsync(policy, default);

        Assert.Equal(PolicyResultState.RollbackFailed, report.State);
        Assert.NotEqual(PolicyResultState.Compliant, report.State);
    }

    [Fact]
    public async Task Rollback_RestoresOriginalState()
    {
        var provider = new InMemorySettingProvider();
        await provider.SetAsync("k", SettingValue.Present("original"), default);
        PolicyEngine engine = CreateEngine(provider, out _);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("k", "desired")]);

        await engine.ApplyAsync(policy, default);
        Assert.Equal("desired", (await provider.GetAsync("k", default)).Value);

        PolicyExecutionReport rollback = await engine.RollbackAsync("p", default);

        Assert.Equal(PolicyResultState.Applied, rollback.State);
        Assert.Equal("original", (await provider.GetAsync("k", default)).Value);
    }

    [Fact]
    public async Task Rollback_WithNoRestorePoint_ReturnsUnknown()
    {
        PolicyEngine engine = CreateEngine(new InMemorySettingProvider(), out _);

        PolicyExecutionReport report = await engine.RollbackAsync("never-applied", default);

        Assert.Equal(PolicyResultState.Unknown, report.State);
    }

    [Fact]
    public async Task Apply_AuditsWithCorrelationId()
    {
        var provider = new InMemorySettingProvider();
        PolicyEngine engine = CreateEngine(provider, out RecordingEventStore events);
        IPolicy policy = TestPolicy.Create("p", [TestPolicy.Present("k", "1")]);

        PolicyExecutionReport report = await engine.ApplyAsync(policy, default);

        Assert.Contains(events.Events, e => e.Category == "policy" && e.Message.Contains(report.CorrelationId, StringComparison.Ordinal));
    }
}
