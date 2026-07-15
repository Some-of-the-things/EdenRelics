using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;
using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Data.Tests;

public class ArchiveTests
{
    private static ToolDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ToolDbContext>()
            .UseInMemoryDatabase("tool_" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task Garment_PersistsEvidenceAndEstimate_WithConfirmationAndTimestamps()
    {
        using ToolDbContext db = NewDb();
        Garment garment = new()
        {
            Title = "Cut-label dress",
            SellerRef = "seller-1",
            Evidence =
            {
                // Machine-proposed evidence defaults to Proposed (brief §3.6).
                new EvidenceRecord { Type = EvidenceType.CareLabel, Feature = "care.tumble-dry-symbol", ImageKey = "labels/a.jpg" },
                // Human-confirmed evidence.
                new EvidenceRecord { Type = EvidenceType.PhoneNumber, Feature = "phone.london-01", RawValue = "01-234 5678", Origin = "human", Confirmation = ConfirmationState.Confirmed },
            },
            Estimates =
            {
                new DateEstimate { Earliest = 1980, Latest = 1986, Outcome = "Estimated", EvidenceChainJson = "[{\"ruleId\":\"CARE-TD\"}]", ComputedAtUtc = DateTime.UtcNow },
            },
        };
        db.Garments.Add(garment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Garment loaded = await db.Garments
            .Include(g => g.Evidence)
            .Include(g => g.Estimates)
            .FirstAsync(g => g.Id == garment.Id);

        Assert.Equal(2, loaded.Evidence.Count);
        Assert.Single(loaded.Estimates);
        Assert.Equal((1980, 1986), (loaded.Estimates[0].Earliest, loaded.Estimates[0].Latest));
        Assert.True(loaded.CreatedAtUtc > default(DateTime));
        Assert.Equal(ConfirmationState.Proposed, loaded.Evidence.First(e => e.Feature == "care.tumble-dry-symbol").Confirmation);
        Assert.Equal(ConfirmationState.Confirmed, loaded.Evidence.First(e => e.Feature == "phone.london-01").Confirmation);
    }

    [Fact]
    public async Task EvidenceRecord_ConfirmationTransition_Persists()
    {
        using ToolDbContext db = NewDb();
        Garment garment = new() { Evidence = { new EvidenceRecord { Type = EvidenceType.Zip, Feature = "zip.nylon-coil" } } };
        db.Garments.Add(garment);
        await db.SaveChangesAsync();

        EvidenceRecord evidence = garment.Evidence[0];
        evidence.Confirmation = ConfirmationState.Confirmed;
        evidence.ConfirmedBy = "teodora";
        evidence.ConfirmedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        EvidenceRecord reloaded = await db.EvidenceRecords.FirstAsync(e => e.Id == evidence.Id);
        Assert.Equal(ConfirmationState.Confirmed, reloaded.Confirmation);
        Assert.Equal("teodora", reloaded.ConfirmedBy);
        Assert.NotNull(reloaded.ConfirmedAtUtc);
    }

    [Fact]
    public async Task DbRuleStore_ReturnsOnlyVerifiedRules_ProjectedToDomain()
    {
        using ToolDbContext db = NewDb();
        db.StoredRules.AddRange(
            new StoredRule { Id = "V1", Feature = "f.a", NotBefore = 1980, Strength = BoundStrength.Hard, SourceCitation = "src", Status = RuleStatus.Verified },
            new StoredRule { Id = "U1", Feature = "f.b", NotBefore = 2000, Strength = BoundStrength.Hard, Status = RuleStatus.Unverified });
        await db.SaveChangesAsync();

        IReadOnlyList<DatingRule> rules = new DbRuleStore(db).VerifiedRules();

        DatingRule only = Assert.Single(rules);
        Assert.Equal("V1", only.Id);
        Assert.Equal(1980, only.NotBefore);
        Assert.Equal(BoundStrength.Hard, only.Strength);
    }

    [Fact]
    public async Task Engine_OverDbBackedRules_ProducesTheWorkedExample()
    {
        using ToolDbContext db = NewDb();
        db.StoredRules.AddRange(
            new StoredRule { Id = "CARE-TD", Feature = "care.tumble-dry-symbol", NotBefore = 1980, Strength = BoundStrength.Hard, Status = RuleStatus.Verified },
            new StoredRule { Id = "CARE-WT", Feature = "care.numbered-wash-tub", NotAfter = 1986, Strength = BoundStrength.Hard, Status = RuleStatus.Verified },
            new StoredRule { Id = "PHONE-01", Feature = "phone.london-01", NotAfter = 1990, Strength = BoundStrength.Hard, Status = RuleStatus.Verified });
        await db.SaveChangesAsync();

        IDatingEngine engine = new DatingEngine(new DbRuleStore(db));
        DatingResult result = engine.Estimate(
        [
            new Evidence("care.tumble-dry-symbol", EvidenceType.CareLabel),
            new Evidence("care.numbered-wash-tub", EvidenceType.CareLabel),
            new Evidence("phone.london-01", EvidenceType.PhoneNumber),
        ], claim: new DateInterval(1970, 1979));

        Assert.Equal(new DateInterval(1980, 1986), result.Range);
        Assert.NotNull(result.ClaimFlag);
        Assert.Equal(BoundStrength.Hard, result.ClaimFlag!.Strength);
    }
}
