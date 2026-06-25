using System.Text.Json.Serialization;

namespace Sovereign.Contracts.Ipc;

/// <summary>
/// Source-generated JSON serialization context for the IPC contract types. Using a generated
/// context keeps serialization trim- and self-contained-safe and avoids reflection-based
/// serialization (ADR 0002).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HelloRequest))]
[JsonSerializable(typeof(HelloResponse))]
[JsonSerializable(typeof(RequestEnvelope))]
[JsonSerializable(typeof(ResponseEnvelope))]
[JsonSerializable(typeof(HealthStatus))]
[JsonSerializable(typeof(EventRecord))]
[JsonSerializable(typeof(QueryEventsRequest))]
[JsonSerializable(typeof(QueryEventsResponse))]
[JsonSerializable(typeof(PolicyInfo))]
[JsonSerializable(typeof(PolicyListResult))]
[JsonSerializable(typeof(PolicyChangeInfo))]
[JsonSerializable(typeof(PolicyPlanInfo))]
[JsonSerializable(typeof(PolicyDetectResult))]
[JsonSerializable(typeof(PolicyRunResult))]
[JsonSerializable(typeof(PolicyTargetRequest))]
public sealed partial class IpcJsonContext : JsonSerializerContext
{
}
