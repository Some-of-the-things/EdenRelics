namespace EdenRelics.SellerTool.Dating;

/// <summary>Source of the dating rules. The engine is a generic machine over this store (brief §3.4),
/// so it can be built and tested before the real rules exist — seed with fixtures now, pour Teodora's
/// verified rules in later. A DB-backed store replaces the in-memory one without touching the engine.</summary>
public interface IRuleStore
{
    /// <summary>Only verified rules — unverified rules must never affect output (brief §3.4).</summary>
    IReadOnlyList<DatingRule> VerifiedRules();

    /// <summary>Documented coexistence periods. An unverified group is as inert as an unverified rule.</summary>
    IReadOnlyList<TransitionGroup> TransitionGroups();
}

public sealed class InMemoryRuleStore(
    IEnumerable<DatingRule> rules,
    IEnumerable<TransitionGroup>? groups = null) : IRuleStore
{
    private readonly IReadOnlyList<DatingRule> _verified =
        rules.Where(r => r.Status == RuleStatus.Verified).ToList();

    private readonly IReadOnlyList<TransitionGroup> _groups =
        (groups ?? []).Where(g => g.Status == RuleStatus.Verified).ToList();

    public IReadOnlyList<DatingRule> VerifiedRules() => _verified;

    public IReadOnlyList<TransitionGroup> TransitionGroups() => _groups;
}
