using Sovereign.Contracts.Ipc;

namespace Sovereign.Service;

/// <summary>
/// Options for <see cref="NamedPipeServer"/>. The pipe name is configurable primarily so tests can
/// run isolated servers on unique names; production uses the contract default.
/// </summary>
internal sealed class PipeServerOptions
{
    /// <summary>The pipe name to listen on (without the <c>\\.\pipe\</c> prefix).</summary>
    public string PipeName { get; init; } = IpcContract.PipeName;
}
