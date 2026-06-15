using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Data;

/// <summary>
/// Idempotently seeds the launch ("Tier-1") vintage-care entries so the reviewer has a
/// ready worklist: Viyella as a full AI draft to review, the rest as stubs carrying their
/// target head terms and review notes. Each entry is added only if its slug is absent, so
/// this is safe to run on every startup and won't clobber edits.
/// </summary>
public static class CareSeed
{
    public static async Task EnsureSeedDataAsync(EdenRelicsDbContext db)
    {
        await SeedFabricsAsync(db);
        await SeedIssuesAsync(db);
    }

    private static async Task SeedFabricsAsync(EdenRelicsDbContext db)
    {
        List<CareFabric> wanted =
        [
            new CareFabric
            {
                Slug = "viyella",
                Name = "Viyella",
                AlsoKnownAs = ["Viyella twill", "winter cotton"],
                TargetKeywords =
                [
                    "viyella fabric", "viyella fabric uk", "viyella care",
                    "how to wash viyella", "is viyella washable", "viyella fabric care"
                ],
                Intro =
                    "Viyella is a heritage British fabric — a soft blend of merino wool and long-staple " +
                    "cotton first produced near Matlock, Derbyshire, in 1893, and often described as the world's " +
                    "first branded fabric. Warm yet light and far more forgiving than pure wool, it turns up " +
                    "constantly in vintage shirts, blouses, dresses and nightwear, including pieces from Marks & " +
                    "Spencer's St Michael era.",
                FiberContent =
                    "Traditionally 55% merino wool / 45% cotton; many later and modern weaves are 80% cotton / 20% wool.",
                HowToIdentify =
                    "A fine, soft twill with a gently brushed handle — warm but lightweight. Look for the woven " +
                    "Viyella selvedge or label; older pieces feel more wool-forward than cotton-rich modern ones.",
                Washing =
                    "Treat it as a delicate. Hand wash in cool water (around 30°C) with a wool-friendly or " +
                    "delicates detergent. A sturdier modern piece can go on a wool/delicate machine cycle, cool, " +
                    "inside a mesh bag. Always colour-test a vintage piece first — older dyes can bleed.",
                Drying =
                    "Never tumble dry — heat and agitation felt the wool content and shrink it. Press the water " +
                    "out in a towel (don't wring) and dry flat, away from direct heat and sunlight.",
                Ironing =
                    "Iron slightly damp on a warm/wool setting, ideally under a pressing cloth to protect the surface.",
                Storing =
                    "Clean before storing — moths are drawn to wool fibres and to food/skin traces. Fold with " +
                    "acid-free tissue or hang on a padded hanger; keep cool, dark and dry, and use cedar or lavender " +
                    "rather than mothballs.",
                VintageCautions =
                    "Older Viyella carries more wool and decades of wear, so it's more delicate and more shrink- and " +
                    "moth-prone than modern cotton-rich versions. Test for dye-fastness, check seams and any elastic, " +
                    "and when a piece is fragile or structured, spot-clean or have it professionally cleaned rather " +
                    "than risk a full wash.",
                Dos =
                [
                    "Hand wash cool with a wool-friendly detergent",
                    "Dry flat, away from heat",
                    "Colour-test vintage pieces first",
                    "Store clean, with cedar or lavender"
                ],
                Donts =
                [
                    "Tumble dry",
                    "Wring or hang it sopping wet",
                    "Use hot water or chlorine bleach",
                    "Store it unwashed (invites moths)"
                ],
                MetaTitle = "How to Care for Vintage Viyella — Washing, Drying & Storage",
                MetaDescription =
                    "A practical guide to caring for vintage Viyella, the classic British wool-cotton blend: how to " +
                    "wash, dry, iron and store it without shrinking or felting.",
                Status = CareReviewStatus.AiDrafted,
                ReviewNotes =
                    "AI draft — please verify before publishing: (1) the wool/cotton blend ratios (older 55/45 " +
                    "wool-cotton vs modern 80/20 cotton-wool), (2) the safe wash temperature, (3) anything specific to " +
                    "M&S St Michael Viyella. Add a first-hand tip if you have one.",
                IsPublished = false,
            },
            new CareFabric
            {
                Slug = "rayon",
                Name = "Rayon",
                AlsoKnownAs = ["viscose", "art silk"],
                TargetKeywords =
                [
                    "rayon dress", "rayon maxi dresses", "vintage rayon care",
                    "how to wash vintage rayon", "does rayon shrink"
                ],
                MetaTitle = "How to Care for Vintage Rayon",
                Status = CareReviewStatus.Draft,
                ReviewNotes =
                    "Stub — key angle: crepe rayon (dry-clean / will shrink and stiffen) vs plain-weave rayon " +
                    "(hand-washable cool). GSC-proven demand. Cover dye-bleed testing and removing shoulder pads first.",
                IsPublished = false,
            },
            new CareFabric
            {
                Slug = "lace",
                Name = "Lace",
                AlsoKnownAs = ["antique lace", "vintage lace"],
                TargetKeywords =
                [
                    "how to clean vintage lace", "washing antique lace", "vintage lace care"
                ],
                MetaTitle = "How to Clean and Care for Vintage Lace",
                Status = CareReviewStatus.Draft,
                ReviewNotes =
                    "Stub — delicate-handling angle: gentle hand wash, support the weight when wet, never bleach, " +
                    "vinegar for rust spots, dry flat, store with acid-free tissue.",
                IsPublished = false,
            },
        ];

        foreach (CareFabric fabric in wanted)
        {
            bool exists = await db.CareFabrics.AnyAsync(f => f.Slug == fabric.Slug);
            if (!exists)
            {
                db.CareFabrics.Add(fabric);
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedIssuesAsync(EdenRelicsDbContext db)
    {
        List<CareIssue> wanted =
        [
            new CareIssue
            {
                Slug = "yellow-age-stains",
                Name = "Removing yellow age stains",
                AlsoKnownAs = ["age yellowing", "oxidation stains"],
                TargetKeywords =
                [
                    "how to remove yellow age stains from vintage clothing",
                    "yellow stains vintage white fabric", "age yellowing vintage"
                ],
                MetaTitle = "Removing Yellow Age Stains From Vintage Clothing",
                Status = CareReviewStatus.Draft,
                ReviewNotes =
                    "Stub — highest-intent, weakest competition. Cover oxygen-bleach soak, hydrogen-peroxide " +
                    "paste, lemon + sun; WARN that heat sets stains; give per-fabric safety (silk/wool vs cotton/linen).",
                IsPublished = false,
            },
            new CareIssue
            {
                Slug = "musty-smell",
                Name = "Getting the musty smell out of vintage clothes",
                AlsoKnownAs = ["storage smell", "old clothes smell"],
                TargetKeywords =
                [
                    "how to get musty smell out of vintage clothes",
                    "remove smell from vintage clothing", "vintage clothes smell"
                ],
                MetaTitle = "How to Get the Musty Smell Out of Vintage Clothes",
                Status = CareReviewStatus.Draft,
                ReviewNotes =
                    "Stub — fragmented SERP. Cover airing/sunlight, baking-soda bagging, vinegar, vodka spray, " +
                    "enzyme wash; caution on delicate fabrics and direct sun.",
                IsPublished = false,
            },
            new CareIssue
            {
                Slug = "moth-hole-repair",
                Name = "Repairing moth holes in vintage wool",
                AlsoKnownAs = ["mending moth holes", "moth damage repair"],
                TargetKeywords =
                [
                    "how to repair moth holes in vintage wool",
                    "mend moth holes knitwear", "fix moth holes"
                ],
                MetaTitle = "Repairing Moth Holes in Vintage Wool",
                Status = CareReviewStatus.Draft,
                ReviewNotes =
                    "Stub — restoration-expertise angle: hand stitching, felting small holes, patching, when to " +
                    "use a professional reweaver; plus prevention (clean before storing, cedar/lavender, freezing eggs).",
                IsPublished = false,
            },
        ];

        foreach (CareIssue issue in wanted)
        {
            bool exists = await db.CareIssues.AnyAsync(i => i.Slug == issue.Slug);
            if (!exists)
            {
                db.CareIssues.Add(issue);
            }
        }
        await db.SaveChangesAsync();
    }
}
