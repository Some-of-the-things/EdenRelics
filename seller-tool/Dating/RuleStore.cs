namespace EdenRelics.SellerTool.Dating;

/// <summary>Source of the dating rules. The engine is a generic machine over this store (brief §3.4),
/// so it can be built and tested before the real rules exist — seed with fixtures now, pour Teodora's
/// verified rules in later. A DB-backed store replaces the in-memory one without touching the engine.</summary>
public interface IRuleStore
{
    /// <summary>Only verified rules — unverified rules must never affect output (brief §3.4).</summary>
    IReadOnlyList<DatingRule> VerifiedRules();
}

public sealed class InMemoryRuleStore(IEnumerable<DatingRule> rules) : IRuleStore
{
    private readonly IReadOnlyList<DatingRule> _verified =
        rules.Where(r => r.Status == RuleStatus.Verified).ToList();

    public IReadOnlyList<DatingRule> VerifiedRules() => _verified;
}
