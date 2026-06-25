namespace Sovereign.Ipc;

/// <summary>
/// Pure protocol-version negotiation logic, separated so it can be unit-tested without any
/// transport (ADR 0002).
/// </summary>
public static class ProtocolNegotiation
{
    /// <summary>
    /// Computes the highest protocol version common to the client and service ranges.
    /// </summary>
    /// <param name="clientMin">Client's lowest supported version.</param>
    /// <param name="clientMax">Client's highest supported version.</param>
    /// <param name="serviceMin">Service's lowest supported version.</param>
    /// <param name="serviceMax">Service's highest supported version.</param>
    /// <param name="agreedVersion">The negotiated version on success; otherwise 0.</param>
    /// <returns><see langword="true"/> if a common version exists; otherwise <see langword="false"/> (fail closed).</returns>
    public static bool TryNegotiate(int clientMin, int clientMax, int serviceMin, int serviceMax, out int agreedVersion)
    {
        agreedVersion = 0;

        if (clientMin <= 0 || clientMax < clientMin || serviceMin <= 0 || serviceMax < serviceMin)
        {
            return false;
        }

        int low = Math.Max(clientMin, serviceMin);
        int high = Math.Min(clientMax, serviceMax);

        if (high < low)
        {
            return false;
        }

        agreedVersion = high;
        return true;
    }
}
