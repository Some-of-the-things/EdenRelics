using EdenRelics.SellerTool.Dating;
using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Data;

/// <summary>Database-backed rule store: the dating engine reads its rules from here, so rules can be
/// added/edited without shipping the engine. Only Verified rules are returned (brief §3.4).</summary>
public sealed class DbRuleStore(ToolDbContext db) : IRuleStore
{
    public IReadOnlyList<DatingRule> VerifiedRules() =>
        db.StoredRules
            .Where(r => r.Status == RuleStatus.Verified)
            .AsEnumerable()
            .Select(r => r.ToDomain())
            .ToList();
}
