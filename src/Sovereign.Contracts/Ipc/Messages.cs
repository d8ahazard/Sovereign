using System.Collections.Generic;

namespace Sovereign.Contracts.Ipc;

/// <summary>
/// The first message a client sends, declaring the protocol version range it supports.
/// </summary>
/// <param name="ClientProtocolMin">Lowest protocol version the client supports.</param>
/// <param name="ClientProtocolMax">Highest protocol version the client supports.</param>
/// <param name="ClientName">A short, non-authoritative client name for audit/logging.</param>
public sealed record HelloRequest(int ClientProtocolMin, int ClientProtocolMax, string ClientName);

/// <summary>
/// The service's reply to a <see cref="HelloRequest"/>.
/// </summary>
/// <param name="Accepted">Whether a common protocol version was agreed.</param>
/// <param name="AgreedProtocolVersion">The negotiated version when <paramref name="Accepted"/> is true; otherwise 0.</param>
/// <param name="ServiceVersion">The service's informational version string.</param>
/// <param name="RejectReason">Why negotiation failed, when <paramref name="Accepted"/> is false.</param>
public sealed record HelloResponse(bool Accepted, int AgreedProtocolVersion, string ServiceVersion, string? RejectReason);

/// <summary>
/// A point-in-time summary of service health.
/// </summary>
/// <param name="ServiceVersion">The service's informational version string.</param>
/// <param name="ProtocolVersion">The protocol version the service prefers.</param>
/// <param name="State">A short state label (for example, <c>Running</c>).</param>
/// <param name="StartedUtc">When the current service instance started.</param>
/// <param name="UptimeSeconds">Seconds since <paramref name="StartedUtc"/>.</param>
/// <param name="EventCount">Total audit events recorded in the local store.</param>
public sealed record HealthStatus(
    string ServiceVersion,
    int ProtocolVersion,
    string State,
    DateTimeOffset StartedUtc,
    long UptimeSeconds,
    long EventCount);

/// <summary>
/// A single append-only audit event.
/// </summary>
/// <param name="Id">Monotonic local identifier.</param>
/// <param name="TimestampUtc">When the event was recorded.</param>
/// <param name="Category">Stable category identifier.</param>
/// <param name="Message">Human-readable description (must not contain secrets).</param>
public sealed record EventRecord(long Id, DateTimeOffset TimestampUtc, string Category, string Message);

/// <summary>
/// Parameters for a <see cref="IpcOperation.QueryEvents"/> request.
/// </summary>
/// <param name="Limit">Maximum number of events to return (server clamps to a safe bound).</param>
/// <param name="AfterId">When set, only events with an id greater than this value are returned.</param>
public sealed record QueryEventsRequest(int Limit, long? AfterId);

/// <summary>
/// The payload returned for a <see cref="IpcOperation.QueryEvents"/> request.
/// </summary>
/// <param name="Events">The matching events, most recent last.</param>
public sealed record QueryEventsResponse(IReadOnlyList<EventRecord> Events);

/// <summary>
/// The envelope for an operation request issued after a successful hello.
/// </summary>
/// <param name="RequestId">Client-assigned id echoed back in the response.</param>
/// <param name="Operation">The requested operation.</param>
/// <param name="Query">Payload for <see cref="IpcOperation.QueryEvents"/>; otherwise null.</param>
public sealed record RequestEnvelope(long RequestId, IpcOperation Operation, QueryEventsRequest? Query);

/// <summary>
/// The envelope for an operation response.
/// </summary>
/// <param name="RequestId">The id from the corresponding <see cref="RequestEnvelope"/>.</param>
/// <param name="ErrorCode">Result code; <see cref="IpcErrorCode.None"/> means success.</param>
/// <param name="Message">Optional human-readable detail, typically for errors.</param>
/// <param name="Health">Payload for <see cref="IpcOperation.GetHealth"/>; otherwise null.</param>
/// <param name="Events">Payload for <see cref="IpcOperation.QueryEvents"/>; otherwise null.</param>
/// <param name="Version">Payload for <see cref="IpcOperation.GetVersion"/>; otherwise null.</param>
public sealed record ResponseEnvelope(
    long RequestId,
    IpcErrorCode ErrorCode,
    string? Message,
    HealthStatus? Health,
    QueryEventsResponse? Events,
    string? Version);
