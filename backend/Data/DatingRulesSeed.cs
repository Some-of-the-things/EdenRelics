using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Data;

/// <summary>
/// The starting rule set, so the engine has something real to run against before the
/// research lands.
///
/// The telephone family is the solid one — UK numbering changes are documented, dated
/// public record, and they date the *label*, not the brand, so they work on cut-label
/// garments. The care-symbol rules here are the handful with a clear provenance.
///
/// IMPORTANT: everything seeded as <see cref="RuleStatus.Unverified"/> is inert by
/// design. Do not promote a rule to Active without a primary source in its citation —
/// a tool that flags a correct listing as wrong is worse than one that flags nothing.
/// </summary>
public static class DatingRulesSeed
{
    /// <summary>Inserts any seed rule whose Code is not already present. Never overwrites edits.</summary>
    public static async Task EnsureSeedRulesAsync(EdenRelicsDbContext db, CancellationToken ct = default)
    {
        List<DatingRule> seed = BuildSeed();
        HashSet<string> existing = await db.DatingRules
            .IgnoreQueryFilters()
            .Select(r => r.Code)
            .ToHashSetAsync(ct);

        List<DatingRule> missing = seed.Where(r => !existing.Contains(r.Code)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        foreach (DatingRule rule in missing)
        {
            rule.Id = Guid.CreateVersion7();
            rule.CreatedAtUtc = now;
            rule.UpdatedAtUtc = now;
        }

        db.DatingRules.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The seed rules as data. Public so tests can assert on the shipped content directly —
    /// a mis-scoped regex or a rule promoted to Active without a source is a content bug,
    /// and content bugs in here flag correct listings as wrong.
    /// </summary>
    public static List<DatingRule> BuildSeed() =>
    [
        // ── Telephone family (verified) ──────────────────────────────────────
        // These date the printed label rather than the garment's design, which is exactly
        // what makes them useful on a piece with the brand label cut out.
        new DatingRule
        {
            Code = "PHONE_UK_BARE_01_LONDON",
            Description =
                "A bare '01' London telephone number was only valid until London split into 071/081 in May 1990.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            // 01 followed by a separator and more digits, but NOT 01x1 (the post-1995
            // provincial format such as 0161 Manchester), which would otherwise match.
            TestValue = @"^0\s*1[\s\-]?(?!\d1)\d",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1990, 5, 6),
            Strength = RuleStrength.Hard,
            SourceCitation = "UK Phone Day / London 01 split to 071 and 081, 6 May 1990.",
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_071_081_LONDON",
            Description =
                "A 071 or 081 London number only existed between the May 1990 split and PhONEday in April 1995.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*[78]1[\s\-]?\d",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1990, 5, 6),
            Strength = RuleStrength.Hard,
            SourceCitation = "London 071/081 introduced 6 May 1990.",
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_071_081_PHONEDAY",
            Description =
                "071/081 numbers were replaced by 0171/0181 at PhONEday on 16 April 1995.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*[78]1[\s\-]?\d",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1995, 4, 16),
            Strength = RuleStrength.Hard,
            SourceCitation = "PhONEday, 16 April 1995: 0X1 numbers became 01X1.",
            Status = RuleStatus.Active,
        },

        // ── Care-label symbol family ─────────────────────────────────────────
        new DatingRule
        {
            Code = "CARE_TUMBLE_DRY_SYMBOL",
            Description =
                "The tumble-drying symbol (a circle within a square) entered the international care code around 1980; "
                + "it cannot appear on a garment labelled before then.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "tumble_dry_symbol",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1980, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "International textile care labelling code — tumble-dry symbol adoption, c.1980.",
            Status = RuleStatus.Active,
            ResearchNotes = "Pin the exact BS/ISO revision and date before relying on this in a guarantee.",
        },
        new DatingRule
        {
            Code = "CARE_NUMBERED_WASH_TUB",
            Description =
                "Numbered wash-tub symbols (the UK 1-9 process numbers) were superseded by temperature-only tubs "
                + "in the mid-1980s.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "numbered_wash_tub",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1986, 12, 31),
            Strength = RuleStrength.Hard,
            SourceCitation = "UK HLCC numbered wash-code system, withdrawn mid-1980s.",
            Status = RuleStatus.Active,
            ResearchNotes = "HLCC withdrawal date needs a primary source; 1986 is the working figure.",
        },
        new DatingRule
        {
            Code = "CARE_LABEL_ABSENT",
            Description =
                "No care label at all is weakly suggestive of a pre-1966 garment, before care labelling became "
                + "common in the UK.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueEquals,
            TestValue = "absent",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1966, 1, 1),
            // Deliberately SOFT: UK care labelling was never legally mandatory, makers
            // lagged the standard, and labels get cut out by wearers. Absence proves very
            // little, and a system that cannot represent that is the thing we differentiate
            // against.
            Strength = RuleStrength.Soft,
            SourceCitation = "HLCC care labelling scheme introduced 1960s; adoption voluntary and gradual.",
            Status = RuleStatus.Active,
        },

        // ── Origin wording ───────────────────────────────────────────────────
        new DatingRule
        {
            Code = "ORIGIN_WEST_GERMANY",
            Description =
                "\"Made in West Germany\" cannot post-date reunification on 3 October 1990.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "west germany",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1990, 10, 3),
            Strength = RuleStrength.Hard,
            SourceCitation = "German reunification, 3 October 1990.",
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_BRITISH_EMPIRE_MADE",
            Description = "\"Empire Made\" wording is characteristic of pre-1960s British Commonwealth labelling.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "empire made",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1965, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — needs a primary source before this can be relied on.",
            // Inert until sourced. Present so the research can be staged, not so it can be used.
            Status = RuleStatus.Unverified,
            ResearchNotes = "Trade description practice, not legislation. Find a dated primary reference.",
        },
    ];
}
