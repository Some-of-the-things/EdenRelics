using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Data;

/// <summary>
/// The shipped rule set, encoding "Eden Relics Dating Rules" v0.3 (Teodora, July 2026).
///
/// The document is the source of truth for content; this file is its encoding. Every rule
/// carries its <see cref="DatingRule.SpecId"/> so the two can be reviewed against each
/// other, and its <see cref="DatingRule.Provenance"/> so the engine knows what each claim
/// rests on rather than only how hard it is.
///
/// Two principles from the document govern everything here, and both are load-bearing:
///
/// PRESENCE IS HARD, ABSENCE IS SOFT. A symbol that did not exist before a year cannot
/// appear on a garment made before it — fact, not judgement. But the absence of a care
/// label proves almost nothing: UK care labelling was never legally mandatory, makers
/// lagged, and labels get cut out.
///
/// NEVER ENCODE AN UNVERIFIED RULE AS ACTIVE. Rules sourced only to trade press or
/// community consensus are seeded inert so the research can be staged. Twelve rules we can
/// defend beat thirty we cannot, and a tool that flags a correct listing as wrong is worse
/// than one that flags nothing.
/// </summary>
public static class DatingRulesSeed
{
    /// <summary>
    /// Inserts missing seed rules and groups, and upgrades ones nobody has edited.
    ///
    /// Insert-only is not enough: the research document is explicitly a living file, and
    /// v0.3 revised the sourcing and provenance of rules that v0.2 had already shipped. A
    /// correction that cannot reach the rows already in the database is not a correction.
    ///
    /// A row that has been edited since it was seeded is left completely alone — the
    /// research UI is meant to win over the seed file, and silently reverting a
    /// researcher's work would make the tool untrustworthy in exactly the way it exists to
    /// avoid.
    /// </summary>
    public static async Task EnsureSeedRulesAsync(EdenRelicsDbContext db, CancellationToken ct = default)
    {
        await SyncGroupsAsync(db, ct);
        await SyncRulesAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SyncRulesAsync(EdenRelicsDbContext db, CancellationToken ct)
    {
        List<DatingRule> seed = BuildSeed();
        Dictionary<string, DatingRule> existing = await db.DatingRules
            .IgnoreQueryFilters()
            .ToDictionaryAsync(r => r.Code, ct);

        DateTime now = DateTime.UtcNow;
        foreach (DatingRule rule in seed)
        {
            if (!existing.TryGetValue(rule.Code, out DatingRule? current))
            {
                rule.Id = Guid.CreateVersion7();
                rule.CreatedAtUtc = now;
                rule.UpdatedAtUtc = now;
                db.DatingRules.Add(rule);
                continue;
            }

            if (current.UpdatedAtUtc != current.CreatedAtUtc)
            {
                continue;
            }

            current.SpecId = rule.SpecId;
            current.Description = rule.Description;
            current.EvidenceType = rule.EvidenceType;
            current.TestKind = rule.TestKind;
            current.TestValue = rule.TestValue;
            current.BoundType = rule.BoundType;
            current.BoundDate = rule.BoundDate;
            current.Strength = rule.Strength;
            current.SourceCitation = rule.SourceCitation;
            current.Provenance = rule.Provenance;
            current.Status = rule.Status;
            current.TrailingToleranceMonths = rule.TrailingToleranceMonths;
            current.TransitionGroupCode = rule.TransitionGroupCode;
            current.ResearchNotes = rule.ResearchNotes;
            current.CreatedAtUtc = now;
            current.UpdatedAtUtc = now;
        }
    }

    private static async Task SyncGroupsAsync(EdenRelicsDbContext db, CancellationToken ct)
    {
        List<DatingTransitionGroup> seed = BuildTransitionGroups();
        Dictionary<string, DatingTransitionGroup> existing = await db.DatingTransitionGroups
            .IgnoreQueryFilters()
            .ToDictionaryAsync(g => g.Code, ct);

        DateTime now = DateTime.UtcNow;
        foreach (DatingTransitionGroup group in seed)
        {
            if (!existing.TryGetValue(group.Code, out DatingTransitionGroup? current))
            {
                group.Id = Guid.CreateVersion7();
                group.CreatedAtUtc = now;
                group.UpdatedAtUtc = now;
                db.DatingTransitionGroups.Add(group);
                continue;
            }

            if (current.UpdatedAtUtc != current.CreatedAtUtc)
            {
                continue;
            }

            current.Description = group.Description;
            current.PeriodStart = group.PeriodStart;
            current.PeriodEnd = group.PeriodEnd;
            current.TrailingToleranceMonths = group.TrailingToleranceMonths;
            current.SourceCitation = group.SourceCitation;
            current.Provenance = group.Provenance;
            current.Status = group.Status;
            current.CreatedAtUtc = now;
            current.UpdatedAtUtc = now;
        }
    }

    /// <summary>
    /// Documented periods in which two conventions genuinely coexisted. See
    /// <see cref="DatingTransitionGroup"/> for why these widen rather than narrow.
    /// </summary>
    public static List<DatingTransitionGroup> BuildTransitionGroups() =>
    [
        new DatingTransitionGroup
        {
            Code = "CARE-1986",
            Description =
                "Numbered wash tubs and temperature wash tubs coexisted across the 1980-1986 UK care-label "
                + "generations, alternating between the 1980 and 1982 codes. Finding both dates a garment to the "
                + "changeover, not to the instant the standard changed.",
            PeriodStart = new DateOnly(1980, 1, 1),
            PeriodEnd = new DateOnly(1986, 12, 31),
            SourceCitation =
                "Oxfordshire Museums / Dress & Textile Specialists, 'Dating Collections Using Standardised Wash "
                + "Codes' — six-column GINETEX symbol table showing both conventions across 1980-1986.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
        },
    ];

    /// <summary>
    /// The seed rules as data. Public so tests can assert on the shipped content directly —
    /// a mis-scoped regex, or a rule promoted to Active without a source, is a content bug,
    /// and content bugs in here flag correct listings as wrong.
    /// </summary>
    public static List<DatingRule> BuildSeed() =>
    [
        // ── §1 Care label symbols (UK) ───────────────────────────────────────
        // The richest deterministic family in post-war British clothing, and as of v0.3 the
        // best-provenanced. The sequence is HLCC 1966 -> HLCC 1976/77 -> BS 2747:1980 ->
        // HLCC 1982 -> BS 2747:1986 -> BS EN 23758:1994. The standard is BS 2747, NOT
        // BS 2427: that number is a long-standing transposition error for an unrelated
        // engineering standard, and it should not be reintroduced from older notes.
        new DatingRule
        {
            Code = "CARE_SYMBOL_LABEL_PRESENT",
            SpecId = "CARE-01",
            Description =
                "A symbol-based care label cannot pre-date 1966, when the HLCC introduced the first standardised "
                + "UK symbol system.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "symbol",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1966, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation =
                "HLCC introduced standardised UK symbol-based wash codes in 1966. Oxfordshire Museums / Dress & "
                + "Textile Specialists fact sheet: 'symbol based wash codes have evolved since 1966'.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "The converse (no care label therefore pre-1966) is a different and much weaker rule — see "
                + "CARE_LABEL_ABSENT.",
        },
        new DatingRule
        {
            Code = "CARE_WHITE_ON_COLOURED_WASH_SYMBOL_FLOOR",
            SpecId = "CARE-02",
            Description =
                "A wash symbol printed white on a coloured or black background, with all other instructions in "
                + "text, is the first-generation HLCC layout.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "white_on_coloured_wash_symbol",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1966, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "First-generation HLCC layout, 1966. Monochrome printing arrived at the 1976 revision. "
                + "Oxfordshire Museums / Dress & Textile Specialists fact sheet.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes = "An era signal, not a lock — hence Soft at both ends. Paired with the _CAP rule.",
        },
        new DatingRule
        {
            Code = "CARE_WHITE_ON_COLOURED_WASH_SYMBOL_CAP",
            SpecId = "CARE-02",
            Description =
                "The white-on-coloured wash symbol layout gave way to monochrome symbols at the 1976 HLCC revision.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "white_on_coloured_wash_symbol",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1976, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "The fact sheet dates the monochrome shift to 1976 specifically. Oxfordshire Museums / Dress & "
                + "Textile Specialists.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "CARE_NUMBERED_TUB_WITHOUT_TEMPERATURES",
            SpecId = "CARE-02b",
            Description =
                "A numbered wash tub with no temperature shown characterises the 1966 and 1976/77 generations, "
                + "before temperatures were added.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "numbered_wash_tub_no_temperature",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1980, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "Numbered-only tubs characterise the 1966 and 1976/77 generations. Oxfordshire Museums / Dress & "
                + "Textile Specialists six-column table.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "Numbers ALONE lean early, but the number system itself persisted to 1986 — that is CARE-05's "
                + "job, and this rule must not be confused with it. Deliberately NOT a CARE-1986 member: the "
                + "absence of temperatures is what dates this, and it is the opposite of a co-occurrence.",
        },
        new DatingRule
        {
            Code = "CARE_HLCC_1982_INTERIM",
            SpecId = "CARE-06b",
            Description =
                "The HLCC 1982 interim code sits between BS 2747:1980 and :1986, with numbered tubs returning "
                + "alongside temperatures and refined dry-clean and iron symbols.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "hlcc_1982_features",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1982, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "PENDING — HLCC 1982 'International Textile Care Labelling Code: What it Means to You'. The fact "
                + "sheet lists 1982 as a distinct generation, but what it changed vs 1980 is not established.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "A genuine but minor generation, and low priority. Inert until someone establishes exactly what "
                + "1982 altered — without that the test value cannot be defined precisely enough to fire correctly.",
        },
        new DatingRule
        {
            Code = "CARE_IRON_DRYCLEAN_BLEACH_SYMBOLS_UK",
            SpecId = "CARE-03a",
            Description =
                "Ironing, dry-cleaning and bleaching symbols entered the UK system at the 1976 HLCC revision, so a "
                + "UK-made garment carrying them cannot be earlier.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "iron_symbol",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1976, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation =
                "UK (HLCC) introduced ironing, dry-cleaning and bleaching symbols with monochrome printing in 1976; "
                + "GINETEX five-symbol set launched 1975.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "Applies only where UK manufacture is established. Continental European makers reportedly used these "
                + "symbols earlier — see CARE_IRON_SYMBOLS_CONTINENTAL, which is inert pending a source.",
        },
        new DatingRule
        {
            Code = "CARE_IRON_SYMBOLS_CONTINENTAL",
            SpecId = "CARE-03b",
            Description =
                "Continental European makers are reported to have used the care symbols from the late 1960s, ahead "
                + "of the UK.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "iron_symbol",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1968, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — GINETEX 50th-anniversary material is the primary source; not yet read.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "'Europe' is too vague to encode. Which countries, from when? Inert until the GINETEX material is read.",
        },
        new DatingRule
        {
            Code = "CARE_TUMBLE_DRY_SYMBOL",
            SpecId = "CARE-04",
            Description =
                "Drying symbols first appear at the BS 2747:1980 generation, so a tumble-dryer symbol rules out any "
                + "1970s claim.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "tumble_dry_symbol",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1980, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation =
                "BS 2747:1980 (Textile Care Labelling Code). The Oxfordshire Museums six-column table shows the "
                + "DRYING row empty before 1980 and populated from 1980 on.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "One of the cleanest single-symbol bounds. Primary confirmation (BS 2747:1980 itself, on BSOL or "
                + "legal deposit at Cambridge UL) would finalise it but is no longer urgent — corroborated across "
                + "three independent sources.",
        },
        new DatingRule
        {
            Code = "CARE_NUMBERED_WASH_TUB",
            SpecId = "CARE-05",
            Description =
                "Numbered wash tubs (the UK 1-9 process numbers) ran to the BS 2747:1986 generation, when "
                + "temperatures with dots and underlines replaced them.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "numbered_wash_tub",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1986, 12, 31),
            Strength = RuleStrength.Hard,
            SourceCitation =
                "Numbered tubs run across the 1966, 1976/77, 1980 and 1982 generations; replaced at BS 2747:1986. "
                + "Oxfordshire Museums / Dress & Textile Specialists six-column table.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            // Hard on the leading edge of the change, lagged on the trailing: printed label
            // stock got used up long after the standard moved.
            TransitionGroupCode = "CARE-1986",
            ResearchNotes =
                "Co-occurrence with CARE_WASH_TUB_UNDERLINE is NOT a contradiction — the conventions overlapped, "
                + "which is what the CARE-1986 transition group represents.",
        },
        new DatingRule
        {
            Code = "CARE_WASH_TUB_UNDERLINE",
            SpecId = "CARE-06",
            Description =
                "Agitation underlines beneath the wash tub or dry-clean circle were introduced at BS 2747:1986.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "wash_tub_underline",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1986, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation =
                "BS 2747:1986 (Code of Practice for Textile Care Labelling) introduced agitation underlines and "
                + "temperature tubs. Oxfordshire Museums / Dress & Textile Specialists fact sheet.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            TransitionGroupCode = "CARE-1986",
            ResearchNotes =
                "In this generation the 'double' underline is a BROKEN single line, not two stacked layers — the "
                + "two-layer form is a later and separate signal (CARE_DOUBLE_UNDERLINE_STACKED).",
        },
        new DatingRule
        {
            Code = "CARE_DOUBLE_UNDERLINE_STACKED",
            SpecId = "CARE-07",
            Description =
                "The double underline drawn as two stacked layers, rather than as a broken single line, is reported "
                + "to be a c.2005 change.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "double_underline_stacked",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2005, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — reported change from broken to two-layer underline c.2005.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Unverified,
            ResearchNotes = "Verify. Would usefully separate 1990s from 2000s stock if it holds.",
        },
        new DatingRule
        {
            Code = "CARE_SYMBOL_ORDER_ISO_1994",
            SpecId = "CARE-08",
            Description =
                "The UK adopted ISO 3758 as BS EN 23758:1994, fixing the symbol sequence wash, bleach, iron, "
                + "dry-clean, dry until the 2005 reordering.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "order_wash_bleach_iron_dryclean_dry",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1994, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "BS EN 23758:1994, from ISO 3758:1991. UK merged with the ISO system in 1994.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "CARE_SYMBOL_ORDER_PROCESS_2005",
            SpecId = "CARE-09",
            Description = "Symbols in process order (wash, bleach, dry, iron, dry-clean) are reported to follow a 2005 reordering.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "order_wash_bleach_dry_iron_dryclean",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2005, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — reported 2005 reordering.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Unverified,
            ResearchNotes = "Verify. Mainly useful for ruling garments OUT of vintage.",
        },
        new DatingRule
        {
            Code = "CARE_MODERN_SQUARED_SYMBOL_SET",
            SpecId = "CARE-10",
            Description = "A squared-off, size-consistent modern symbol set is reported to follow a 2012 redesign.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "modern_squared_symbols",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2012, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — reported 2012 resizing and squaring of the symbol set.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Unverified,
            ResearchNotes = "Verify. The cleanest 'not vintage' test in the family if confirmed.",
        },
        new DatingRule
        {
            Code = "CARE_TEXT_ONLY_INSTRUCTIONS",
            SpecId = "CARE-11",
            Description =
                "Care instructions in words only, with no symbols, suggest a garment predating the spread of "
                + "symbol labelling.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueEquals,
            TestValue = "text_only",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1979, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "Oxfordshire Museums fact sheet: the 1950s were text-only and ad-hoc before 1966 standardisation.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "The specific WORDING claim — that phrases like 'machine wash warm' follow a timeline — remains an "
                + "unsourced lead. Do not encode it.",
        },
        new DatingRule
        {
            Code = "CARE_LABEL_ABSENT",
            SpecId = "CARE-01 (converse)",
            Description =
                "No care label at all weakly suggests a garment predating the 1966 introduction of UK symbol "
                + "labelling.",
            EvidenceType = EvidenceType.CareLabel,
            TestKind = EvidenceTestKind.ValueEquals,
            TestValue = "absent",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1966, 1, 1),
            // Deliberately SOFT and weak. UK care labelling was never legally mandatory,
            // makers lagged the standard, and labels get cut out by wearers. Absence proves
            // very little, and a system that cannot represent that is the thing we
            // differentiate against.
            Strength = RuleStrength.Soft,
            SourceCitation = "HLCC care labelling introduced 1966; adoption voluntary and gradual, never mandatory.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes = "Check for a cut-out label before letting this influence anything.",
        },

        // ── §2 Fibre content labelling ───────────────────────────────────────
        // Legally mandatory in the UK, which is what makes absence meaningful here in a way
        // it is not elsewhere. Every date below is primary legislation, read directly.
        new DatingRule
        {
            Code = "FIBRE_CONTENT_ABSENT",
            SpecId = "FIBRE-01",
            Description =
                "No fibre content indication on a garment sold new at UK retail suggests it predates the 1986-87 "
                + "staging of mandatory content labelling.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueEquals,
            TestValue = "no_fibre_content",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1987, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "Textile Products (Indications of Fibre Content) Regulations 1986 (SI 1986/26), staged into force "
                + "1986-87; SI 1973/2124 already required content labelling.",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Active,
            ResearchNotes = "Strong for an absence rule, but check for a cut label first.",
        },
        new DatingRule
        {
            Code = "FIBRE_LYOCELL_TENCEL",
            SpecId = "FIBRE-02",
            Description = "'Lyocell' and 'Tencel' were only added to the permitted fibre names in 1998.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\b(lyocell|tencel)\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1998, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "SI 1998/1169 (read directly).",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Active,
            ResearchNotes = "Best-value fibre rule — common in 2000s stock, and a clean modern filter.",
        },
        new DatingRule
        {
            Code = "FIBRE_ELASTANE",
            SpecId = "FIBRE-03",
            Description = "'Elastane' entered the permitted fibre names in 1973.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\belastane\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1973, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "SI 1973/2124 Schedule 2 item 37 (read directly).",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Active,
            ResearchNotes = "Low value in practice — a 1973 floor catches almost nothing in our stock.",
        },
        new DatingRule
        {
            Code = "FIBRE_ELASTOMULTIESTER_POLYLACTIDE",
            SpecId = "FIBRE-04",
            Description = "'Elastomultiester' and 'polylactide' were added to the permitted fibre names in 2006.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\b(elastomultiester|polylactide)\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2006, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Directive 2006/2/EC and the 2008/121 correlation table (read directly).",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "FIBRE_MELAMINE",
            SpecId = "FIBRE-05",
            Description = "'Melamine' first appears as a permitted fibre name in Regulation (EU) 1007/2011.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\bmelamine\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2011, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Absent from the 2009 recast; first appears in Regulation (EU) 1007/2011.",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "FIBRE_NON_TEXTILE_ANIMAL_PARTS",
            SpecId = "FIBRE-06",
            Description =
                "The phrase 'contains non-textile parts of animal origin' was required from 8 May 2012.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "non-textile parts of animal origin",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2012, 5, 8),
            Strength = RuleStrength.Hard,
            SourceCitation = "Regulation (EU) 1007/2011, applicable 8 May 2012.",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Active,
            ResearchNotes = "Clean modern-garment filter.",
        },
        new DatingRule
        {
            Code = "FIBRE_PERCENTAGE_COMPOSITION_FORMAT",
            SpecId = "FIBRE-07",
            Description =
                "Fibre composition stated as exact percentages by weight, rather than informal descriptors, "
                + "suggests labelling under the 1973 content regulations or later.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\d{1,3}\s*%",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1973, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "PENDING — Textile Products (Indications of Fibre Content) Regulations 1973 (SI 1973/2124) "
                + "introduced mandatory composition labelling; whether they mandated percentage BREAKDOWN is not "
                + "yet extracted.",
            Provenance = ProvenanceClass.PrimaryLegislation,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "HIGHEST-YIELD FOLLOW-UP IN THE PROJECT, and nearly free: SI 1973/2124 is already in hand (it is "
                + "where elastane was confirmed at item 37). One more read of its Schedule settles this. If "
                + "percentage format was mandated, promote to Hard/Active — it then becomes a rare vintage-WINDOW "
                + "rule, unlike the rest of the fibre family which only filters modern stock.",
        },
        new DatingRule
        {
            Code = "FIBRE_INFORMAL_COMPOSITION_WORDING",
            SpecId = "FIBRE-08",
            Description =
                "Informal composition wording such as 'All Wool' or 'Pure Cotton', with no percentage breakdown, "
                + "leans towards a pre-1973 garment.",
            EvidenceType = EvidenceType.Fabric,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\b(all|pure)\s+(wool|cotton|silk|linen|new\s+wool)\b",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1973, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation =
                "Informal descriptors characterise 1950s-60s labelling, before percentage composition became "
                + "standard practice.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "ONE-DIRECTIONAL ONLY, and this encoding does not yet express that. 'Pure new wool' and "
                + "'100% cotton' persist to the present day, so informal wording may lean early but must NEVER cap "
                + "late. Inert until the engine can carry a lean that is not also a bound — promoting it as-is "
                + "would wrongly rule out modern garments.",
        },
        // DECLINED, recorded so it is not re-proposed: the 'other fibres exceeding 5%
        // dates a label to 1973-1986' claim. The thresholds actually read in Directive
        // 2008/121 are 10% and 15%, with a tolerance regime far more tangled than a single
        // window, so the tidy version contradicts primary text already in hand.

        // ── §3 Certification marks ───────────────────────────────────────────
        new DatingRule
        {
            Code = "MARK_WOOLMARK",
            SpecId = "MARK-01",
            Description = "The Woolmark logo was launched internationally in 1964.",
            EvidenceType = EvidenceType.CertificationMark,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "woolmark",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1964, 8, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Woolmark launched internationally, August 1964.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "A floor only — the logo has not changed since. Absence proves nothing: the scheme is opt-in.",
        },
        new DatingRule
        {
            Code = "MARK_WOOLBLEND",
            SpecId = "MARK-02",
            Description = "The Woolmark Blend / Woolblend mark was introduced in 1971.",
            EvidenceType = EvidenceType.CertificationMark,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"wool\s?blend",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1971, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Woolblend mark introduced 1971; a 30-49% variant added 1999.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes = "The 1999 variant, if it can be distinguished, would give a later floor.",
        },
        new DatingRule
        {
            Code = "MARK_CC41_UTILITY",
            SpecId = "MARK-03",
            Description =
                "The CC41 Utility mark was applied under the wartime Utility Scheme from September 1941.",
            EvidenceType = EvidenceType.CertificationMark,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "cc41",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1941, 9, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Utility Scheme mark introduced September 1941.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "MARK_CC41_UTILITY_END",
            SpecId = "MARK-03",
            Description = "CC41 Utility labelling continued to around 1952, after rationing ended in 1949.",
            EvidenceType = EvidenceType.CertificationMark,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "cc41",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1952, 12, 31),
            Strength = RuleStrength.Hard,
            SourceCitation = "SOURCE CONFLICT: rationing ended 1949, but VFG reports labelling continuing to 1952.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Active,
            ResearchNotes =
                "Encoded at the WIDER end of the conflict deliberately — an over-tight bound would flag correct "
                + "listings as wrong. Resolve via IWM / National Archives, then narrow if justified.",
        },
        new DatingRule
        {
            Code = "MARK_CC41_SUPER_UTILITY_X",
            SpecId = "MARK-04",
            Description = "A CC41 code with an 'X' prefix is reported to indicate Super Utility, from January 1948.",
            EvidenceType = EvidenceType.CertificationMark,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\bX\d{2,4}(/\d+)?\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1948, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — 'X' reported to indicate Super Utility, introduced January 1948.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Unverified,
        },
        new DatingRule
        {
            Code = "MARK_LYCRA",
            SpecId = "MARK-05",
            Description = "Lycra was introduced by DuPont in 1962, though UK womenswear adoption at scale came later.",
            EvidenceType = EvidenceType.CertificationMark,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "lycra",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1962, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — DuPont elastane brand, 1962. UK womenswear adoption date not established.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes = "The only DuPont mark worth pursuing for our stock. Confirm UK womenswear adoption.",
        },

        // ── §4 Telephone and fax numbers ─────────────────────────────────────
        // The most satisfying family: UK numbering changed on precise, documented dates,
        // and these date the printed LABEL, which is exactly what makes them work on a
        // garment with the brand label cut out.
        new DatingRule
        {
            Code = "PHONE_UK_BARE_01_LONDON",
            SpecId = "TEL-01",
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
            SourceCitation = "London 01 split into 071 and 081, 6 May 1990.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_071_081_LONDON",
            SpecId = "TEL-02",
            Description =
                "A 071 or 081 London number only existed between the May 1990 split and PhONEday in April 1995.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*[78]1[\s\-]?\d",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1990, 5, 6),
            Strength = RuleStrength.Hard,
            SourceCitation = "London 071/081 introduced 6 May 1990.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes = "A five-year window from a phone number — paired with PHONE_UK_071_081_PHONEDAY.",
        },
        new DatingRule
        {
            Code = "PHONE_UK_071_081_PHONEDAY",
            SpecId = "TEL-02",
            Description = "071/081 numbers were replaced by 0171/0181 at PhONEday on 16 April 1995.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*[78]1[\s\-]?\d",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1995, 4, 16),
            Strength = RuleStrength.Hard,
            SourceCitation = "PhONEday, 16 April 1995: 0X1 numbers became 01X1.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_PHONEDAY_01X1",
            SpecId = "TEL-03",
            Description =
                "PhONEday inserted a '1' after the initial '0' in every UK geographic code on 16 April 1995, so an "
                + "'01' + digit code cannot pre-date it.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*1\d{2,3}[\s\-]?\d",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1995, 4, 16),
            Strength = RuleStrength.Hard,
            SourceCitation = "PhONEday, 16 April 1995.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes =
                "A small leading tolerance would be justified for the 1994 parallel-running period; the engine "
                + "currently applies leading tolerance nowhere, which errs on the safe side for a presence bound.",
        },
        new DatingRule
        {
            Code = "PHONE_UK_FOUR_DIGIT_CITY_CODES",
            SpecId = "TEL-04",
            Description =
                "Bristol, Leeds, Leicester, Nottingham and Sheffield received new four-digit codes (0117, 0113, "
                + "0116, 0115, 0114) at PhONEday in 1995.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*11[34567]\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1995, 4, 16),
            Strength = RuleStrength.Hard,
            SourceCitation = "Five cities received new four-digit codes at PhONEday, 16 April 1995.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_LONDON_020",
            SpecId = "TEL-05",
            Description = "London 020 7 / 020 8 numbers date from the Big Number Change on 22 April 2000.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*20\s?[78]",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2000, 4, 22),
            Strength = RuleStrength.Hard,
            SourceCitation = "Big Number Change, 22 April 2000.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes = "Definitively not vintage.",
        },
        new DatingRule
        {
            Code = "PHONE_UK_MOBILE_AND_SERVICE_RANGES",
            SpecId = "TEL-06",
            Description = "The 07, 08 and 09 mobile and service ranges were allocated from 1997.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^0\s*(7[789]|845|870|9\d)",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1997, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "07/08/09 ranges allocated from 1997.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_INTERNATIONAL_010",
            SpecId = "TEL-07",
            Description = "The international access code was '010' until PhONEday replaced it with '00' in 1995.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"^010\s?\d",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1995, 4, 16),
            Strength = RuleStrength.Hard,
            SourceCitation = "International access code 010 became 00 at PhONEday, 16 April 1995.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "PHONE_UK_FAX_PRESENT",
            SpecId = "TEL-08",
            Description = "A fax number alongside a phone number suggests a label from the mid-1980s or later.",
            EvidenceType = EvidenceType.PhoneNumber,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "fax",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1985, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — UK small-business fax adoption accelerated mid-to-late 1980s.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "A fax number also inherits every TEL rule: one in 071 format carries the 1990-95 window "
                + "regardless of this rule's own status.",
        },

        // ── §5 Country of origin and place-name wording ──────────────────────
        // Geopolitics dates garments. All presence rules, therefore hard.
        new DatingRule
        {
            Code = "ORIGIN_WEST_GERMANY",
            SpecId = "GEO-01",
            Description = "'Made in West Germany' cannot post-date reunification on 3 October 1990.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"west(ern)?\s+germany",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1990, 10, 3),
            Strength = RuleStrength.Hard,
            SourceCitation = "German reunification, 3 October 1990.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_CZECHOSLOVAKIA",
            SpecId = "GEO-02",
            Description = "'Made in Czechoslovakia' cannot post-date the dissolution on 1 January 1993.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "czechoslovakia",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1992, 12, 31),
            Strength = RuleStrength.Hard,
            SourceCitation = "Dissolution of Czechoslovakia, 1 January 1993.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_CZECH_REPUBLIC_SLOVAKIA",
            SpecId = "GEO-02b",
            Description =
                "'Made in the Czech Republic' or 'Slovakia' cannot pre-date the successor states, 1 January 1993.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"czech\s+republic|\bslovakia\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1993, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Czech Republic and Slovakia became successor states on 1 January 1993.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_USSR",
            SpecId = "GEO-03",
            Description = "'Made in the USSR' cannot post-date the dissolution in December 1991.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\bu\.?s\.?s\.?r\.?\b|soviet union",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1991, 12, 26),
            Strength = RuleStrength.Hard,
            SourceCitation = "Dissolution of the USSR, December 1991.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_EAST_GERMANY",
            SpecId = "GEO-03b",
            Description = "'Made in the GDR' or 'East Germany' cannot post-date reunification on 3 October 1990.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\bg\.?d\.?r\.?\b|east\s+germany|german\s+democratic",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1990, 10, 3),
            Strength = RuleStrength.Hard,
            SourceCitation = "The GDR existed 1949 to 3 October 1990.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes = "Appears in knitwear.",
        },
        new DatingRule
        {
            Code = "ORIGIN_EAST_GERMANY_FLOOR",
            SpecId = "GEO-03b",
            Description = "'Made in the GDR' or 'East Germany' cannot pre-date the founding of the GDR in 1949.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\bg\.?d\.?r\.?\b|east\s+germany|german\s+democratic",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1949, 10, 7),
            Strength = RuleStrength.Hard,
            SourceCitation = "The GDR was founded 7 October 1949.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_YUGOSLAVIA",
            SpecId = "GEO-04",
            Description = "'Made in Yugoslavia' cannot post-date the breakup of 1991-92.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "yugoslavia",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1992, 12, 31),
            Strength = RuleStrength.Hard,
            SourceCitation = "Breakup of Yugoslavia, 1991-92.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_SERBIA_AND_MONTENEGRO",
            SpecId = "GEO-04",
            Description =
                "'Serbia and Montenegro' names the state union that existed only between 2003 and 2006.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"serbia\s+(and|&|/)\s*montenegro",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2003, 2, 4),
            Strength = RuleStrength.Hard,
            SourceCitation = "The State Union of Serbia and Montenegro existed 4 February 2003 to 5 June 2006.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes = "A three-year window, and a definitive 'not vintage' signal.",
        },
        new DatingRule
        {
            Code = "ORIGIN_SERBIA_AND_MONTENEGRO_CAP",
            SpecId = "GEO-04",
            Description = "The Serbia and Montenegro state union dissolved in June 2006.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"serbia\s+(and|&|/)\s*montenegro",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(2006, 6, 5),
            Strength = RuleStrength.Hard,
            SourceCitation = "Montenegro declared independence 3 June 2006; the union dissolved 5 June 2006.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_HONG_KONG_COLONIAL",
            SpecId = "GEO-05",
            Description =
                "British colonial wording for Hong Kong cannot post-date the handover on 1 July 1997.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"(british\s+)?crown\s+colony|british\s+hong\s+kong",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1997, 6, 30),
            Strength = RuleStrength.Hard,
            SourceCitation = "Handover of Hong Kong, 1 July 1997.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes =
                "HIGH PRIORITY to tighten: 'British Crown Colony' phrasing may indicate c.1950s-1983, far tighter "
                + "than 1997, and it is common in 60s-80s UK high-street clothing.",
        },
        new DatingRule
        {
            Code = "ORIGIN_EEC",
            SpecId = "GEO-06",
            Description = "'Made in the EEC' cannot post-date the EEC becoming the EU on 1 November 1993.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\be\.?e\.?c\.?\b",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1993, 11, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "The EEC became the EU on 1 November 1993.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_EU",
            SpecId = "GEO-06",
            Description = "'Made in the EU' cannot pre-date the EEC becoming the EU on 1 November 1993.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"made\s+in\s+(the\s+)?e\.?u\.?\b|european\s+union",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1993, 11, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "The EEC became the EU on 1 November 1993.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_CEYLON",
            SpecId = "GEO-07",
            Description = "'Made in Ceylon' cannot post-date the 1972 renaming to Sri Lanka.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "ceylon",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1972, 5, 22),
            Strength = RuleStrength.Hard,
            SourceCitation = "Ceylon became Sri Lanka in 1972.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_BURMA",
            SpecId = "GEO-08",
            Description = "'Made in Burma' cannot post-date the 1989 renaming to Myanmar.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "burma",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1989, 6, 18),
            Strength = RuleStrength.Hard,
            SourceCitation = "Burma was renamed Myanmar in 1989.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_RHODESIA",
            SpecId = "GEO-09",
            Description = "'Made in Rhodesia' cannot post-date the 1980 renaming to Zimbabwe.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "rhodesia",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1980, 4, 18),
            Strength = RuleStrength.Hard,
            SourceCitation = "Rhodesia became Zimbabwe in 1980.",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
        },
        new DatingRule
        {
            Code = "ORIGIN_SPLIT_ATTRIBUTION",
            SpecId = "GEO-10",
            Description =
                "Split attribution such as 'Designed in Italy, Made in China' reflects offshored production and "
                + "suggests c.1990 or later.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"designed\s+in\s+\w+.{0,30}made\s+in\s+\w+",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1990, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — no source establishes a date for this convention.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes = "Research lead only. Do not activate without a source.",
        },
        // GEO-11 ('Made in Great Britain / England / Wales / Scotland') is deliberately NOT
        // encoded. Country wording alone dates nothing; it is a brand-layer signal that
        // belongs in maker dossiers. Recorded here so nobody adds it later in good faith.
        new DatingRule
        {
            Code = "ORIGIN_BRITISH_EMPIRE_MADE",
            SpecId = "GEO (unlisted)",
            Description = "'Empire Made' wording is characteristic of pre-1960s British Commonwealth labelling.",
            EvidenceType = EvidenceType.OriginText,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = "empire made",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1965, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — needs a primary source before this can be relied on.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "Trade description practice, not legislation. Not in the v0.3 document; retained from our own "
                + "earlier draft. Find a dated primary reference or retire it.",
        },

        // ── §6 Addresses, postcodes and postal counties ──────────────────────
        // British, granular and unexploited by anyone else in the trade — directly on the moat.
        new DatingRule
        {
            Code = "ADDR_FULL_UK_POSTCODE",
            SpecId = "ADDR-01",
            Description =
                "A full modern-format UK postcode reflects the 1959-74 national rollout, and in practice the later "
                + "part of it.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\b[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}\b",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1959, 1, 1),
            Strength = RuleStrength.Soft,
            SourceCitation = "UK postcodes rolled out area-by-area between 1959 and 1974.",
            Provenance = ProvenanceClass.SecondaryTrade,
            Status = RuleStatus.Active,
            ResearchNotes =
                "HIGH VALUE: a regional introduction-date table (Royal Mail / Postal Museum) would convert this one "
                + "soft rule into dozens of hard regional ones. Encoded at the earliest national date so it cannot "
                + "over-claim in the meantime.",
        },
        new DatingRule
        {
            Code = "ADDR_LONDON_DISTRICT_ONLY",
            SpecId = "ADDR-02",
            Description =
                "A London postal district with no full postcode ('London W1') suggests a pre-postcode address.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"london\s+[A-Z]{1,2}\d{1,2}\s*$",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1979, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — predates full postcode adoption, but no dated source.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes = "Weak alone; may be worth activating once ADDR-01 has regional dates.",
        },
        new DatingRule
        {
            Code = "ADDR_ABBREVIATED_POSTAL_COUNTY",
            SpecId = "ADDR-03",
            Description =
                "Abbreviated postal counties ('Middx', 'Hants', 'Worcs', 'Salop') fell out of required use as "
                + "postcodes became universal.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\b(middx|hants|worcs|salop|beds|bucks|berks|herts|lancs|yorks|notts|staffs|wilts)\b",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1996, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — Royal Mail cessation date for postal counties not established.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "Common on British labels and unused by competitors, so worth the research. Note postal and "
                + "administrative dates differ: Middlesex ceased administratively in 1965 but survived as a postal "
                + "county for decades.",
        },

        new DatingRule
        {
            Code = "ADDR_TELEX_OR_TELEGRAPHIC",
            SpecId = "ADDR-04",
            Description =
                "A telex or telegraphic address is a strong marker of a 1960s-80s trade-facing label.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"\b(telex|telegrams?|telegraphic\s+address)\b",
            BoundType = DateBoundType.NotAfter,
            BoundDate = new DateOnly(1989, 12, 31),
            Strength = RuleStrength.Soft,
            SourceCitation = "PENDING — no dated source for UK telex decline on garment labels.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "Investigate alongside the fax rule (TEL-08); the two bracket the same period from opposite ends. "
                + "'PO Box' on its own carries no date and is deliberately not encoded.",
        },

        // ── §7 Web and email addresses ───────────────────────────────────────
        // The principle is hard — a URL cannot predate the web — but the DATE is the open
        // question, because brands lagged general adoption by years. Encoded conservatively.
        new DatingRule
        {
            Code = "WEB_URL_PRESENT",
            SpecId = "WEB-01",
            Description = "A web address on a garment label cannot pre-date commercial web adoption.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"(https?://|www\.)[a-z0-9\-]+\.[a-z]{2,}",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1995, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "PENDING — the principle is certain, the date is not yet sourced.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
            ResearchNotes =
                "Our own archive will answer this better than any external source: once enough garments are dated "
                + "by other rules, the earliest URL-bearing label in the corpus sets the floor. A rare case where "
                + "OBSERVED-CORPUS will beat anything published.",
        },
        new DatingRule
        {
            Code = "WEB_EMAIL_PRESENT",
            SpecId = "WEB-02",
            Description = "An email address on a garment label cannot pre-date commercial email adoption.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(1995, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "PENDING — probably lags URLs slightly; date not yet sourced.",
            Provenance = ProvenanceClass.CommunityConsensus,
            Status = RuleStatus.Unverified,
        },
        new DatingRule
        {
            Code = "WEB_SOCIAL_HANDLE",
            SpecId = "WEB-03",
            Description =
                "A social-media handle cannot pre-date the platform it names; the earliest relevant launches are "
                + "from 2006.",
            EvidenceType = EvidenceType.Other,
            TestKind = EvidenceTestKind.ValueMatchesRegex,
            TestValue = @"(instagram|facebook|twitter|tiktok|pinterest)\b|@[a-z0-9_.]{3,}",
            BoundType = DateBoundType.NotBefore,
            BoundDate = new DateOnly(2006, 1, 1),
            Strength = RuleStrength.Hard,
            SourceCitation = "Platform launch dates are documented public record (Twitter 2006, Instagram 2010).",
            Provenance = ProvenanceClass.PrimaryRegistry,
            Status = RuleStatus.Active,
            ResearchNotes =
                "Each platform gives its own, later floor — splitting this into per-platform rules would tighten it "
                + "considerably. Filters modern stock out.",
        },

        // §8 (componentry: zips, interfacing, buttons) is deliberately absent. The document
        // is explicit that it is a research programme rather than a rule set: those rules
        // have to emerge from our own dated corpus, which is what the capture archive is
        // for. Encoding guesses now would poison the corpus they are meant to be derived from.
    ];
}
