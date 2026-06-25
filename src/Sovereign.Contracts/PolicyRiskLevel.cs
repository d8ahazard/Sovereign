namespace Sovereign.Contracts;

/// <summary>
/// The risk a policy carries, used to decide how prominently the UI/CLI warns before applying it.
/// </summary>
public enum PolicyRiskLevel
{
    /// <summary>Low risk; easily reversible with no user-visible disruption.</summary>
    Low = 0,

    /// <summary>Moderate risk; may change visible behavior but is reversible.</summary>
    Medium,

    /// <summary>High risk; may require reboot/logoff or affect important functionality.</summary>
    High,
}
