namespace Sovereign.Policy;

/// <summary>
/// An immutable, id-indexed collection of the policies the service exposes.
/// </summary>
public sealed class PolicyCatalog
{
    private readonly Dictionary<string, IPolicy> _byId;

    /// <summary>
    /// Creates a catalog from a set of policies.
    /// </summary>
    /// <param name="policies">The policies to expose. Ids must be unique.</param>
    public PolicyCatalog(IEnumerable<IPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        this._byId = new Dictionary<string, IPolicy>(StringComparer.Ordinal);
        foreach (IPolicy policy in policies)
        {
            if (!this._byId.TryAdd(policy.Metadata.Id, policy))
            {
                throw new ArgumentException($"Duplicate policy id '{policy.Metadata.Id}'.", nameof(policies));
            }
        }
    }

    /// <summary>Gets all policies in declaration order of their ids.</summary>
    public IReadOnlyList<IPolicy> All => this._byId.Values.ToArray();

    /// <summary>
    /// Looks up a policy by id.
    /// </summary>
    /// <param name="id">The policy id.</param>
    /// <param name="policy">The found policy, if any.</param>
    /// <returns>True if found; otherwise false.</returns>
    public bool TryGet(string id, out IPolicy? policy) => this._byId.TryGetValue(id, out policy);
}
