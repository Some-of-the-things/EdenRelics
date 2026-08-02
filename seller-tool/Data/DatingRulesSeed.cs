using EdenRelics.SellerTool.Dating;
using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Data;

/// <summary>
/// The shipped rule set, encoding "Eden Relics Dating Rules" v0.3 (Teodora, July 2026).
///
/// The document is the source of truth for content; this file is its encoding. Every rule carries
/// its <see cref="StoredRule.SpecId"/> so the two can be reviewed against each other, and its
/// <see cref="ProvenanceClass"/> so the engine knows what each claim rests on rather than only how
/// hard it is.
///
/// Two principles from the document govern everything here, and both are load-bearing:
///
/// PRESENCE IS HARD, ABSENCE IS SOFT. A symbol that did not exist before a year cannot appear on a
/// garment made before it — fact, not judgement. But the absence of a care label proves almost
/// nothing: UK care labelling was never legally mandatory, makers lagged, and labels get cut out.
///
/// NEVER SHIP AN UNVERIFIED RULE AS VERIFIED. Rules sourced only to trade press or community
/// consensus are seeded inert so the research can be staged. Twelve rules we can defend beat thirty
/// we cannot, and a tool that flags a correct listing as wrong is worse than one that flags nothing.
/// </summary>
public static class DatingRulesSeed
{
    /// <summary>
    /// Inserts missing rules and groups, and upgrades ones nobody has edited.
    ///
    /// Insert-only is not enough: the research document is explicitly a living file, and revisions
    /// correct rules that earlier versions already shipped. A correction that cannot reach the rows
    /// already in the database is not a correction. Rows whose content differs from the seed are
    /// treated as researcher edits and left completely alone — the rules UI must win over this file,
    /// and silently reverting someone's work would make the tool untrustworthy in exactly the way it
    /// exists to avoid.
    /// </summary>
    public static async Task EnsureSeededAsync(ToolDbContext db, CancellationToken ct = default)
    {
        Dictionary<string, StoredRule> existingRules =
            await db.StoredRules.ToDictionaryAsync(r => r.Id, ct);
        foreach (StoredRule seed in BuildRules())
        {
            if (!existingRules.TryGetValue(seed.Id, out StoredRule? current))
            {
                db.StoredRules.Add(seed);
                continue;
            }
            if (!IsUnedited(current))
            {
                continue;
            }
            Apply(seed, current);
        }

        Dictionary<string, StoredTransitionGroup> existingGroups =
            await db.StoredTransitionGroups.ToDictionaryAsync(g => g.Code, ct);
        foreach (StoredTransitionGroup seed in BuildTransitionGroups())
        {
            if (!existingGroups.TryGetValue(seed.Code, out StoredTransitionGroup? current))
            {
                db.StoredTransitionGroups.Add(seed);
                continue;
            }
            current.Description = seed.Description;
            current.PeriodStart = seed.PeriodStart;
            current.PeriodEnd = seed.PeriodEnd;
            current.TransitionLagMonths = seed.TransitionLagMonths;
            current.SourceCitation = seed.SourceCitation;
            current.Provenance = seed.Provenance;
            current.Status = seed.Status;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A row still matching some previously-shipped seed on the fields a researcher would change.
    /// Deliberately conservative: if in doubt, treat it as edited and leave it.
    /// </summary>
    private static bool IsUnedited(StoredRule current) =>
        BuildRules().Any(s => s.Id == current.Id
            && s.NotBefore == current.NotBefore
            && s.NotAfter == current.NotAfter
            && s.Strength == current.Strength
            && s.Status == current.Status)
        || string.IsNullOrEmpty(current.SpecId);

    private static void Apply(StoredRule seed, StoredRule current)
    {
        current.SpecId = seed.SpecId;
        current.Feature = seed.Feature;
        current.Match = seed.Match;
        current.Pattern = seed.Pattern;
        current.Type = seed.Type;
        current.NotBefore = seed.NotBefore;
        current.NotAfter = seed.NotAfter;
        current.Strength = seed.Strength;
        current.TransitionLagMonths = seed.TransitionLagMonths;
        current.SourceCitation = seed.SourceCitation;
        current.Provenance = seed.Provenance;
        current.TransitionGroup = seed.TransitionGroup;
        current.Status = seed.Status;
        current.ResearchNotes = seed.ResearchNotes;
    }

    /// <summary>Default trailing tolerance: regulations have effective dates, labels do not — printed
    /// stock got used up and finished garments sat in warehouses (rules doc §0.5).</summary>
    private const int DefaultLagMonths = 12;

    public static List<StoredTransitionGroup> BuildTransitionGroups() =>
    [
        new()
        {
            Code = "CARE-1986",
            Description =
                "Numbered wash tubs and temperature wash tubs coexisted across the 1980-1986 UK care-label "
                + "generations, alternating between the 1980 and 1982 codes. Finding both dates a garment to the "
                + "changeover, not to the instant the standard changed.",
            PeriodStart = 1980,
            PeriodEnd = 1986,
            TransitionLagMonths = DefaultLagMonths,
            SourceCitation =
                "Oxfordshire Museums / Dress & Textile Specialists, 'Dating Collections Using Standardised Wash "
                + "Codes' — six-column GINETEX symbol table showing both conventions across 1980-1986.",
            Provenance = ProvenanceClass.SecondaryScholarly,
            Status = RuleStatus.Verified,
        },
    ];

    /// <summary>
    /// The seed rules as data. Public so tests can assert on the shipped content directly — a
    /// mis-scoped regex, or a rule shipped Verified without a source, is a content bug, and content
    /// bugs in here flag correct listings as wrong.
    /// </summary>
    public static List<StoredRule> BuildRules() =>
    [
        // ── §1 Care label symbols (UK) ───────────────────────────────────────
        // The sequence is HLCC 1966 -> HLCC 1976/77 -> BS 2747:1980 -> HLCC 1982 -> BS 2747:1986 ->
        // BS EN 23758:1994. The standard is BS 2747, NOT BS 2427: that number is a long-standing
        // transposition error for an unrelated engineering standard, and must not be reintroduced
        // from older notes. These match on FEATURE codes — care symbols are recognised visually, so
        // the client classifies them before the engine ever sees them.
        Care("CARE_SYMBOL_LABEL_PRESENT", "CARE-01", "care.symbol-label-present",
            notBefore: 1966, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "HLCC introduced standardised UK symbol-based wash codes in 1966. Oxfordshire Museums / Dress & "
            + "Textile Specialists fact sheet: 'symbol based wash codes have evolved since 1966'.",
            ProvenanceClass.SecondaryScholarly,
            notes: "The converse (no care label therefore pre-1966) is a different and much weaker rule — see CARE_LABEL_ABSENT."),

        Care("CARE_WHITE_ON_COLOURED_WASH_SYMBOL", "CARE-02", "care.white-on-coloured-wash-symbol",
            notBefore: 1966, notAfter: 1976, BoundStrength.Soft, RuleStatus.Verified,
            "First-generation HLCC layout, 1966; monochrome printing arrived at the 1976 revision. "
            + "Oxfordshire Museums / Dress & Textile Specialists fact sheet.",
            ProvenanceClass.SecondaryScholarly,
            notes: "An era signal, not a lock — hence Soft at both ends."),

        Care("CARE_NUMBERED_TUB_WITHOUT_TEMPERATURES", "CARE-02b", "care.numbered-wash-tub-no-temperature",
            notBefore: null, notAfter: 1980, BoundStrength.Soft, RuleStatus.Verified,
            "Numbered-only tubs characterise the 1966 and 1976/77 generations. Oxfordshire Museums / Dress & "
            + "Textile Specialists six-column table.",
            ProvenanceClass.SecondaryScholarly,
            notes: "Numbers ALONE lean early, but the number system itself persisted to 1986 — that is CARE-05's "
                + "job. Deliberately NOT a CARE-1986 member: the ABSENCE of temperatures is what dates this, "
                + "which is the opposite of a co-occurrence."),

        Care("CARE_IRON_DRYCLEAN_BLEACH_SYMBOLS_UK", "CARE-03a", "care.iron-dryclean-bleach-symbols",
            notBefore: 1976, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "UK (HLCC) introduced ironing, dry-cleaning and bleaching symbols with monochrome printing in 1976; "
            + "GINETEX five-symbol set launched 1975.",
            ProvenanceClass.SecondaryScholarly,
            notes: "Applies only where UK manufacture is established."),

        Care("CARE_IRON_SYMBOLS_CONTINENTAL", "CARE-03b", "care.iron-symbols-continental",
            notBefore: 1968, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — GINETEX 50th-anniversary material is the primary source; not yet read.",
            ProvenanceClass.SecondaryTrade,
            notes: "'Europe' is too vague to encode. Which countries, from when? Inert until the material is read."),

        Care("CARE_TUMBLE_DRY_SYMBOL", "CARE-04", "care.tumble-dry-symbol",
            notBefore: 1980, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "BS 2747:1980 (Textile Care Labelling Code). The Oxfordshire Museums six-column table shows the "
            + "DRYING row empty before 1980 and populated from 1980 on.",
            ProvenanceClass.SecondaryScholarly,
            notes: "One of the cleanest single-symbol bounds — a dryer symbol kills any 1970s claim. Primary "
                + "confirmation (BS 2747:1980 itself) would finalise it but is no longer urgent."),

        Care("CARE_NUMBERED_WASH_TUB", "CARE-05", "care.numbered-wash-tub",
            notBefore: null, notAfter: 1986, BoundStrength.Hard, RuleStatus.Verified,
            "Numbered tubs run across the 1966, 1976/77, 1980 and 1982 generations; replaced at BS 2747:1986. "
            + "Oxfordshire Museums / Dress & Textile Specialists six-column table.",
            ProvenanceClass.SecondaryScholarly,
            group: "CARE-1986",
            notes: "Co-occurrence with CARE_WASH_TUB_UNDERLINE is NOT a contradiction — the conventions "
                + "overlapped, which is what the CARE-1986 transition group represents."),

        Care("CARE_WASH_TUB_UNDERLINE", "CARE-06", "care.wash-tub-underline",
            notBefore: 1986, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "BS 2747:1986 (Code of Practice for Textile Care Labelling) introduced agitation underlines and "
            + "temperature tubs. Oxfordshire Museums / Dress & Textile Specialists fact sheet.",
            ProvenanceClass.SecondaryScholarly,
            group: "CARE-1986",
            notes: "In this generation the 'double' underline is a BROKEN single line, not two stacked layers — "
                + "the two-layer form is a later and separate signal."),

        Care("CARE_HLCC_1982_INTERIM", "CARE-06b", "care.hlcc-1982-features",
            notBefore: 1982, notAfter: 1986, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — HLCC 1982 'International Textile Care Labelling Code: What it Means to You'. The fact "
            + "sheet lists 1982 as a distinct generation, but what it changed vs 1980 is not established.",
            ProvenanceClass.SecondaryScholarly,
            notes: "A genuine but minor generation, low priority. Inert until someone establishes exactly what "
                + "1982 altered — without that the feature cannot be defined precisely enough to fire correctly."),

        Care("CARE_DOUBLE_UNDERLINE_STACKED", "CARE-07", "care.double-underline-stacked",
            notBefore: 2005, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — reported change from broken to two-layer underline c.2005.",
            ProvenanceClass.SecondaryTrade,
            notes: "Verify. Would usefully separate 1990s from 2000s stock if it holds."),

        Care("CARE_SYMBOL_ORDER_ISO_1994", "CARE-08", "care.order-wash-bleach-iron-dryclean-dry",
            notBefore: 1994, notAfter: null, BoundStrength.Soft, RuleStatus.Verified,
            "BS EN 23758:1994, from ISO 3758:1991. UK merged with the ISO system in 1994.",
            ProvenanceClass.SecondaryScholarly),

        Care("CARE_SYMBOL_ORDER_PROCESS_2005", "CARE-09", "care.order-wash-bleach-dry-iron-dryclean",
            notBefore: 2005, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — reported 2005 reordering.",
            ProvenanceClass.SecondaryTrade,
            notes: "Verify. Mainly useful for ruling garments OUT of vintage."),

        Care("CARE_MODERN_SQUARED_SYMBOL_SET", "CARE-10", "care.modern-squared-symbols",
            notBefore: 2012, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — reported 2012 resizing and squaring of the symbol set.",
            ProvenanceClass.SecondaryTrade,
            notes: "Verify. The cleanest 'not vintage' test in the family if confirmed."),

        Care("CARE_TEXT_ONLY_INSTRUCTIONS", "CARE-11", "care.text-only-instructions",
            notBefore: null, notAfter: 1979, BoundStrength.Soft, RuleStatus.Verified,
            "Oxfordshire Museums fact sheet: the 1950s were text-only and ad-hoc before 1966 standardisation.",
            ProvenanceClass.SecondaryScholarly,
            notes: "The specific WORDING claim — that phrases like 'machine wash warm' follow a timeline — "
                + "remains an unsourced lead. Do not encode it."),

        Care("CARE_LABEL_ABSENT", "CARE-01 (converse)", "care.label-absent",
            notBefore: null, notAfter: 1966, BoundStrength.Soft, RuleStatus.Verified,
            "HLCC care labelling introduced 1966; adoption voluntary and gradual, never mandatory.",
            ProvenanceClass.SecondaryScholarly,
            notes: "Deliberately weak. Care labelling was never legally mandatory in the UK, makers lagged, and "
                + "labels get cut out. Check for a cut-out label before letting this influence anything."),

        // ── §2 Fibre content labelling ───────────────────────────────────────
        // Legally mandatory in the UK, which is what makes absence meaningful here in a way it is not
        // elsewhere. Every date below is primary legislation, read directly. These match on the RAW
        // fibre text, because the datable content is the wording itself.
        Value("FIBRE_CONTENT_ABSENT", "FIBRE-01", EvidenceType.Fabric, MatchKind.Feature, null,
            notBefore: null, notAfter: 1987, BoundStrength.Soft, RuleStatus.Verified,
            "Textile Products (Indications of Fibre Content) Regulations 1986 (SI 1986/26), staged into force "
            + "1986-87; SI 1973/2124 already required content labelling.",
            ProvenanceClass.PrimaryLegislation,
            feature: "fibre.content-absent",
            notes: "Strong for an absence rule, but check for a cut label first."),

        Value("FIBRE_LYOCELL_TENCEL", "FIBRE-02", EvidenceType.Fabric, MatchKind.ValueRegex, @"\b(lyocell|tencel)\b",
            notBefore: 1998, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "SI 1998/1169 (read directly).", ProvenanceClass.PrimaryLegislation,
            notes: "Best-value fibre rule — common in 2000s stock, and a clean modern filter."),

        Value("FIBRE_ELASTANE", "FIBRE-03", EvidenceType.Fabric, MatchKind.ValueRegex, @"\belastane\b",
            notBefore: 1973, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "SI 1973/2124 Schedule 2 item 37 (read directly).", ProvenanceClass.PrimaryLegislation,
            notes: "Low value in practice — a 1973 floor catches almost nothing in our stock."),

        Value("FIBRE_ELASTOMULTIESTER_POLYLACTIDE", "FIBRE-04", EvidenceType.Fabric, MatchKind.ValueRegex,
            @"\b(elastomultiester|polylactide)\b",
            notBefore: 2006, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Directive 2006/2/EC and the 2008/121 correlation table (read directly).",
            ProvenanceClass.PrimaryLegislation),

        Value("FIBRE_MELAMINE", "FIBRE-05", EvidenceType.Fabric, MatchKind.ValueRegex, @"\bmelamine\b",
            notBefore: 2011, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Absent from the 2009 recast; first appears in Regulation (EU) 1007/2011.",
            ProvenanceClass.PrimaryLegislation),

        Value("FIBRE_NON_TEXTILE_ANIMAL_PARTS", "FIBRE-06", EvidenceType.Fabric, MatchKind.ValueContains,
            "non-textile parts of animal origin",
            notBefore: 2012, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Regulation (EU) 1007/2011, applicable 8 May 2012.", ProvenanceClass.PrimaryLegislation,
            notes: "Clean modern-garment filter."),

        Value("FIBRE_PERCENTAGE_COMPOSITION_FORMAT", "FIBRE-07", EvidenceType.Fabric, MatchKind.ValueRegex,
            @"\d{1,3}\s*%",
            notBefore: 1973, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — Textile Products (Indications of Fibre Content) Regulations 1973 (SI 1973/2124) introduced "
            + "mandatory composition labelling; whether they mandated percentage BREAKDOWN is not yet extracted.",
            ProvenanceClass.PrimaryLegislation,
            notes: "HIGHEST-YIELD FOLLOW-UP IN THE PROJECT, and nearly free: SI 1973/2124 is already in hand (it "
                + "is where elastane was confirmed at item 37). One more read of its Schedule settles this. If "
                + "percentage format was mandated, promote to Hard/Verified — it then becomes a rare vintage-WINDOW "
                + "rule, unlike the rest of the fibre family which only filters modern stock."),

        Value("FIBRE_INFORMAL_COMPOSITION_WORDING", "FIBRE-08", EvidenceType.Fabric, MatchKind.ValueRegex,
            @"\b(all|pure)\s+(wool|cotton|silk|linen|new\s+wool)\b",
            notBefore: null, notAfter: 1973, BoundStrength.Soft, RuleStatus.Unverified,
            "Informal descriptors characterise 1950s-60s labelling, before percentage composition became standard.",
            ProvenanceClass.SecondaryTrade,
            notes: "ONE-DIRECTIONAL ONLY, and this encoding does not yet express that. 'Pure new wool' and "
                + "'100% cotton' persist to the present day, so informal wording may lean early but must NEVER cap "
                + "late. Inert until the engine can carry a lean that is not also a bound — promoting it as-is "
                + "would wrongly rule out modern garments."),

        // DECLINED, recorded so it is not re-proposed: the 'other fibres exceeding 5% dates a label
        // to 1973-1986' claim. The thresholds actually read in Directive 2008/121 are 10% and 15%,
        // with a tolerance regime far more tangled than a single window, so the tidy version
        // contradicts primary text already in hand.

        // ── §3 Certification marks ───────────────────────────────────────────
        Value("MARK_WOOLMARK", "MARK-01", EvidenceType.RegulatoryMark, MatchKind.Feature, null,
            notBefore: 1964, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Woolmark launched internationally, August 1964.", ProvenanceClass.SecondaryScholarly,
            feature: "mark.woolmark",
            notes: "A floor only — the logo has not changed since. Absence proves nothing: the scheme is opt-in."),

        Value("MARK_WOOLBLEND", "MARK-02", EvidenceType.RegulatoryMark, MatchKind.Feature, null,
            notBefore: 1971, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Woolblend mark introduced 1971; a 30-49% variant added 1999.", ProvenanceClass.SecondaryScholarly,
            feature: "mark.woolblend",
            notes: "The 1999 variant, if it can be distinguished, would give a later floor."),

        Value("MARK_CC41_UTILITY", "MARK-03", EvidenceType.RegulatoryMark, MatchKind.Feature, null,
            notBefore: 1941, notAfter: 1952, BoundStrength.Hard, RuleStatus.Verified,
            "Utility Scheme mark introduced September 1941. SOURCE CONFLICT on the end: rationing ended 1949, "
            + "but VFG reports labelling continuing to 1952.",
            ProvenanceClass.SecondaryScholarly,
            feature: "mark.cc41",
            lagMonths: 0,
            notes: "Encoded at the WIDER end of the conflict deliberately — an over-tight bound would flag correct "
                + "listings as wrong. Resolve via IWM / National Archives, then narrow if justified."),

        Value("MARK_CC41_SUPER_UTILITY_X", "MARK-04", EvidenceType.RegulatoryMark, MatchKind.ValueRegex,
            @"\bX\d{2,4}(/\d+)?\b",
            notBefore: 1948, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — 'X' reported to indicate Super Utility, introduced January 1948.",
            ProvenanceClass.SecondaryTrade),

        Value("MARK_LYCRA", "MARK-05", EvidenceType.RegulatoryMark, MatchKind.ValueContains, "lycra",
            notBefore: 1962, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — DuPont elastane brand, 1962. UK womenswear adoption date not established.",
            ProvenanceClass.CommunityConsensus,
            notes: "The only DuPont mark worth pursuing for our stock. Confirm UK womenswear adoption."),

        // ── §4 Telephone and fax numbers ─────────────────────────────────────
        // The most satisfying family: UK numbering changed on precise, documented dates, and these
        // date the printed LABEL, which is exactly what makes them work on a garment with the brand
        // label cut out. All match the raw number text.
        Value("PHONE_UK_BARE_01_LONDON", "TEL-01", EvidenceType.PhoneNumber, MatchKind.ValueRegex,
            // 01 followed by a separator and more digits, but NOT 01x1 (the post-1995 provincial
            // format such as 0161 Manchester), which would otherwise match.
            @"^0\s*1[\s\-]?(?!\d1)\d",
            notBefore: null, notAfter: 1990, BoundStrength.Hard, RuleStatus.Verified,
            "London 01 split into 071 and 081, 6 May 1990.", ProvenanceClass.PrimaryRegistry),

        Value("PHONE_UK_071_081_LONDON", "TEL-02", EvidenceType.PhoneNumber, MatchKind.ValueRegex,
            @"^0\s*[78]1[\s\-]?\d",
            notBefore: 1990, notAfter: 1995, BoundStrength.Hard, RuleStatus.Verified,
            "London 071/081 created 6 May 1990; became 0171/0181 at PhONEday, 16 April 1995.",
            ProvenanceClass.PrimaryRegistry,
            notes: "A five-year window from a phone number."),

        Value("PHONE_UK_PHONEDAY_01X1", "TEL-03", EvidenceType.PhoneNumber, MatchKind.ValueRegex,
            @"^0\s*1\d{2,3}[\s\-]?\d",
            notBefore: 1995, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "PhONEday inserted a '1' after the initial '0' in every UK geographic code, 16 April 1995.",
            ProvenanceClass.PrimaryRegistry,
            notes: "A small leading tolerance would be justified for the 1994 parallel-running period; the engine "
                + "applies leading tolerance nowhere, which errs on the safe side for a presence bound."),

        Value("PHONE_UK_FOUR_DIGIT_CITY_CODES", "TEL-04", EvidenceType.PhoneNumber, MatchKind.ValueRegex,
            @"^0\s*11[34567]\b",
            notBefore: 1995, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Bristol, Leeds, Leicester, Nottingham and Sheffield received new four-digit codes at PhONEday.",
            ProvenanceClass.PrimaryRegistry),

        Value("PHONE_UK_LONDON_020", "TEL-05", EvidenceType.PhoneNumber, MatchKind.ValueRegex, @"^0\s*20\s?[78]",
            notBefore: 2000, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Big Number Change, 22 April 2000.", ProvenanceClass.PrimaryRegistry,
            notes: "Definitively not vintage."),

        Value("PHONE_UK_MOBILE_AND_SERVICE_RANGES", "TEL-06", EvidenceType.PhoneNumber, MatchKind.ValueRegex,
            @"^0\s*(7[789]|845|870|9\d)",
            notBefore: 1997, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "07/08/09 ranges allocated from 1997.", ProvenanceClass.PrimaryRegistry),

        Value("PHONE_UK_INTERNATIONAL_010", "TEL-07", EvidenceType.PhoneNumber, MatchKind.ValueRegex, @"^010\s?\d",
            notBefore: null, notAfter: 1995, BoundStrength.Hard, RuleStatus.Verified,
            "International access code 010 became 00 at PhONEday, 16 April 1995.", ProvenanceClass.PrimaryRegistry),

        Value("PHONE_UK_FAX_PRESENT", "TEL-08", EvidenceType.PhoneNumber, MatchKind.ValueContains, "fax",
            notBefore: 1985, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — UK small-business fax adoption accelerated mid-to-late 1980s.",
            ProvenanceClass.CommunityConsensus,
            notes: "A fax number also inherits every TEL rule: one in 071 format carries the 1990-95 window "
                + "regardless of this rule's own status."),

        // ── §5 Country of origin and place-name wording ──────────────────────
        // Geopolitics dates garments. All presence rules, therefore hard.
        Value("ORIGIN_WEST_GERMANY", "GEO-01", EvidenceType.OriginText, MatchKind.ValueRegex, @"west(ern)?\s+germany",
            notBefore: null, notAfter: 1990, BoundStrength.Hard, RuleStatus.Verified,
            "German reunification, 3 October 1990.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_CZECHOSLOVAKIA", "GEO-02", EvidenceType.OriginText, MatchKind.ValueContains, "czechoslovakia",
            notBefore: null, notAfter: 1992, BoundStrength.Hard, RuleStatus.Verified,
            "Dissolution of Czechoslovakia, 1 January 1993.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_CZECH_REPUBLIC_SLOVAKIA", "GEO-02b", EvidenceType.OriginText, MatchKind.ValueRegex,
            @"czech\s+republic|\bslovakia\b",
            notBefore: 1993, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Czech Republic and Slovakia became successor states on 1 January 1993.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_USSR", "GEO-03", EvidenceType.OriginText, MatchKind.ValueRegex, @"\bu\.?s\.?s\.?r\.?\b|soviet union",
            notBefore: null, notAfter: 1991, BoundStrength.Hard, RuleStatus.Verified,
            "Dissolution of the USSR, December 1991.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_EAST_GERMANY", "GEO-03b", EvidenceType.OriginText, MatchKind.ValueRegex,
            @"\bg\.?d\.?r\.?\b|east\s+germany|german\s+democratic",
            notBefore: 1949, notAfter: 1990, BoundStrength.Hard, RuleStatus.Verified,
            "The GDR existed from 7 October 1949 to 3 October 1990.", ProvenanceClass.PrimaryRegistry,
            notes: "Appears in knitwear."),

        Value("ORIGIN_YUGOSLAVIA", "GEO-04", EvidenceType.OriginText, MatchKind.ValueContains, "yugoslavia",
            notBefore: null, notAfter: 1992, BoundStrength.Hard, RuleStatus.Verified,
            "Breakup of Yugoslavia, 1991-92.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_SERBIA_AND_MONTENEGRO", "GEO-04", EvidenceType.OriginText, MatchKind.ValueRegex,
            @"serbia\s+(and|&|/)\s*montenegro",
            notBefore: 2003, notAfter: 2006, BoundStrength.Hard, RuleStatus.Verified,
            "The State Union of Serbia and Montenegro existed 4 February 2003 to 5 June 2006.",
            ProvenanceClass.PrimaryRegistry,
            lagMonths: 0,
            notes: "A three-year window, and a definitive 'not vintage' signal."),

        Value("ORIGIN_HONG_KONG_COLONIAL", "GEO-05", EvidenceType.OriginText, MatchKind.ValueRegex,
            @"(british\s+)?crown\s+colony|british\s+hong\s+kong",
            notBefore: null, notAfter: 1997, BoundStrength.Hard, RuleStatus.Verified,
            "Handover of Hong Kong, 1 July 1997.", ProvenanceClass.PrimaryRegistry,
            notes: "HIGH PRIORITY to tighten: 'British Crown Colony' phrasing may indicate c.1950s-1983, far "
                + "tighter than 1997, and it is common in 60s-80s UK high-street clothing."),

        Value("ORIGIN_EEC", "GEO-06", EvidenceType.OriginText, MatchKind.ValueRegex, @"\be\.?e\.?c\.?\b",
            notBefore: null, notAfter: 1993, BoundStrength.Hard, RuleStatus.Verified,
            "The EEC became the EU on 1 November 1993.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_EU", "GEO-06", EvidenceType.OriginText, MatchKind.ValueRegex,
            @"made\s+in\s+(the\s+)?e\.?u\.?\b|european\s+union",
            notBefore: 1993, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "The EEC became the EU on 1 November 1993.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_CEYLON", "GEO-07", EvidenceType.OriginText, MatchKind.ValueContains, "ceylon",
            notBefore: null, notAfter: 1972, BoundStrength.Hard, RuleStatus.Verified,
            "Ceylon became Sri Lanka in 1972.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_BURMA", "GEO-08", EvidenceType.OriginText, MatchKind.ValueContains, "burma",
            notBefore: null, notAfter: 1989, BoundStrength.Hard, RuleStatus.Verified,
            "Burma was renamed Myanmar in 1989.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_RHODESIA", "GEO-09", EvidenceType.OriginText, MatchKind.ValueContains, "rhodesia",
            notBefore: null, notAfter: 1980, BoundStrength.Hard, RuleStatus.Verified,
            "Rhodesia became Zimbabwe in 1980.", ProvenanceClass.PrimaryRegistry),

        Value("ORIGIN_SPLIT_ATTRIBUTION", "GEO-10", EvidenceType.OriginText, MatchKind.ValueRegex,
            @"designed\s+in\s+\w+.{0,30}made\s+in\s+\w+",
            notBefore: 1990, notAfter: null, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — no source establishes a date for this convention.", ProvenanceClass.CommunityConsensus,
            notes: "Research lead only. Do not activate without a source."),

        // GEO-11 ('Made in Great Britain / England / Wales / Scotland') is deliberately NOT encoded.
        // Country wording alone dates nothing; it is a brand-layer signal that belongs in maker
        // dossiers. Recorded here so nobody adds it later in good faith.

        Value("ORIGIN_BRITISH_EMPIRE_MADE", "GEO (unlisted)", EvidenceType.OriginText, MatchKind.ValueContains,
            "empire made",
            notBefore: null, notAfter: 1965, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — needs a primary source before this can be relied on.", ProvenanceClass.CommunityConsensus,
            notes: "Trade description practice, not legislation. Not in the v0.3 document; retained from our own "
                + "earlier draft. Find a dated primary reference or retire it."),

        // ── §6 Addresses, postcodes and postal counties ──────────────────────
        // British, granular and unexploited by anyone else in the trade — directly on the moat.
        Value("ADDR_FULL_UK_POSTCODE", "ADDR-01", EvidenceType.Other, MatchKind.ValueRegex,
            @"\b[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}\b",
            notBefore: 1959, notAfter: null, BoundStrength.Soft, RuleStatus.Verified,
            "UK postcodes rolled out area-by-area between 1959 and 1974.", ProvenanceClass.SecondaryTrade,
            notes: "HIGH VALUE: a regional introduction-date table (Royal Mail / Postal Museum) would convert this "
                + "one soft rule into dozens of hard regional ones. Encoded at the earliest national date so it "
                + "cannot over-claim in the meantime."),

        Value("ADDR_LONDON_DISTRICT_ONLY", "ADDR-02", EvidenceType.Other, MatchKind.ValueRegex,
            @"london\s+[A-Z]{1,2}\d{1,2}\s*$",
            notBefore: null, notAfter: 1979, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — predates full postcode adoption, but no dated source.", ProvenanceClass.CommunityConsensus,
            notes: "Weak alone; may be worth activating once ADDR-01 has regional dates."),

        Value("ADDR_ABBREVIATED_POSTAL_COUNTY", "ADDR-03", EvidenceType.Other, MatchKind.ValueRegex,
            @"\b(middx|hants|worcs|salop|beds|bucks|berks|herts|lancs|yorks|notts|staffs|wilts)\b",
            notBefore: null, notAfter: 1996, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — Royal Mail cessation date for postal counties not established.",
            ProvenanceClass.CommunityConsensus,
            notes: "Common on British labels and unused by competitors, so worth the research. Postal and "
                + "administrative dates differ: Middlesex ceased administratively in 1965 but survived as a "
                + "postal county for decades."),

        Value("ADDR_TELEX_OR_TELEGRAPHIC", "ADDR-04", EvidenceType.Other, MatchKind.ValueRegex,
            @"\b(telex|telegrams?|telegraphic\s+address)\b",
            notBefore: null, notAfter: 1989, BoundStrength.Soft, RuleStatus.Unverified,
            "PENDING — no dated source for UK telex decline on garment labels.", ProvenanceClass.CommunityConsensus,
            notes: "Investigate alongside the fax rule (TEL-08); the two bracket the same period from opposite "
                + "ends. 'PO Box' on its own carries no date and is deliberately not encoded."),

        // ── §7 Web and email addresses ───────────────────────────────────────
        // The principle is hard — a URL cannot predate the web — but the DATE is the open question,
        // because brands lagged general adoption by years. Encoded conservatively.
        Value("WEB_URL_PRESENT", "WEB-01", EvidenceType.Other, MatchKind.ValueRegex,
            @"(https?://|www\.)[a-z0-9\-]+\.[a-z]{2,}",
            notBefore: 1995, notAfter: null, BoundStrength.Hard, RuleStatus.Unverified,
            "PENDING — the principle is certain, the date is not yet sourced.", ProvenanceClass.CommunityConsensus,
            notes: "Our own archive will answer this better than any external source: once enough garments are "
                + "dated by other rules, the earliest URL-bearing label in the corpus sets the floor. A rare case "
                + "where OBSERVED-CORPUS will beat anything published."),

        Value("WEB_EMAIL_PRESENT", "WEB-02", EvidenceType.Other, MatchKind.ValueRegex,
            @"[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}",
            notBefore: 1995, notAfter: null, BoundStrength.Hard, RuleStatus.Unverified,
            "PENDING — probably lags URLs slightly; date not yet sourced.", ProvenanceClass.CommunityConsensus),

        Value("WEB_SOCIAL_HANDLE", "WEB-03", EvidenceType.Other, MatchKind.ValueRegex,
            @"(instagram|facebook|twitter|tiktok|pinterest)\b|@[a-z0-9_.]{3,}",
            notBefore: 2006, notAfter: null, BoundStrength.Hard, RuleStatus.Verified,
            "Platform launch dates are documented public record (Twitter 2006, Instagram 2010).",
            ProvenanceClass.PrimaryRegistry,
            notes: "Each platform gives its own, later floor — splitting this into per-platform rules would "
                + "tighten it considerably. Filters modern stock out."),

        // §8 (componentry: zips, interfacing, buttons) is deliberately absent. The document is
        // explicit that it is a research programme rather than a rule set: those rules have to emerge
        // from our own dated corpus, which is what the capture archive is for. Encoding guesses now
        // would poison the corpus they must be derived from.
    ];

    private static StoredRule Care(
        string id, string specId, string feature, int? notBefore, int? notAfter,
        BoundStrength strength, RuleStatus status, string source, ProvenanceClass provenance,
        string? group = null, string? notes = null) => new()
        {
            Id = id,
            SpecId = specId,
            Feature = feature,
            Match = MatchKind.Feature,
            Type = EvidenceType.CareLabel,
            NotBefore = notBefore,
            NotAfter = notAfter,
            Strength = strength,
            TransitionLagMonths = notAfter is null ? 0 : DefaultLagMonths,
            SourceCitation = source,
            Provenance = provenance,
            TransitionGroup = group,
            Status = status,
            ResearchNotes = notes,
        };

    private static StoredRule Value(
        string id, string specId, EvidenceType type, MatchKind match, string? pattern,
        int? notBefore, int? notAfter, BoundStrength strength, RuleStatus status,
        string source, ProvenanceClass provenance,
        string? feature = null, int? lagMonths = null, string? notes = null) => new()
        {
            Id = id,
            SpecId = specId,
            Feature = feature ?? id.ToLowerInvariant().Replace('_', '.'),
            Match = match,
            Pattern = pattern,
            Type = type,
            NotBefore = notBefore,
            NotAfter = notAfter,
            Strength = strength,
            TransitionLagMonths = lagMonths ?? (notAfter is null ? 0 : DefaultLagMonths),
            SourceCitation = source,
            Provenance = provenance,
            Status = status,
            ResearchNotes = notes,
        };
}
