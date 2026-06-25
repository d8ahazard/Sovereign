namespace Sovereign.Contracts;

/// <summary>
/// The hardening preset a policy belongs to. A preset of level <c>L</c> selects every policy whose
/// <see cref="PolicyLevel"/> is at or below <c>L</c>, so aggressiveness increases Lite &lt; Normal
/// &lt; Pro (ADR 0005).
/// </summary>
public enum PolicyLevel
{
    /// <summary>The most obvious, lowest-risk wins. Included in every preset.</summary>
    Lite = 0,

    /// <summary>The recommended set: Lite plus the broadly safe privacy/performance tweaks.</summary>
    Normal = 1,

    /// <summary>Everything: "I just want a fucking Windows computer."</summary>
    Pro = 2,
}
