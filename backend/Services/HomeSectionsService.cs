using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// The homepage product strips an admin arranges on the Home Sections tab.
///
/// Until the first save there are no rows, and <see cref="Defaults"/> stands in: the two strips the
/// homepage had hard-coded before this existed, with the same wording and order. Deploying therefore
/// changes nothing a customer sees, and the first save from the admin tab takes ownership.
/// </summary>
public class HomeSectionsService(IRepository<HomeSection> sections) : IHomeSectionsService
{
    private const int MaxSections = 12;
    private const int MaxProductsPerSection = 12;
    private const int MaxEyebrow = 80;
    private const int MaxTitle = 120;
    private const int MaxDescription = 500;
    private const int MaxButtonText = 60;
    private const int MaxButtonLink = 200;

    /// <summary>
    /// What a created page's address may look like: one to three lowercase words-with-hyphens
    /// segments, e.g. /autumn-knits or /edits/autumn-knits. No dots, query strings or capitals, so it
    /// can't collide with files (/sitemap.xml) or worker routes (/__sitemap-routes).
    /// </summary>
    private static readonly Regex PagePath = new(
        "^/[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*){0,2}$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// First path segments the site already serves (the routes in frontend app.routes.ts, plus the
    /// API). A section never creates a page under one of these: the site's own page is there, so the
    /// button just links to it. Keep in step with app.routes.ts when adding a route.
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedFirstSegments = new HashSet<string>(StringComparer.Ordinal)
    {
        "shop", "product", "seller", "seller-tool", "admin", "sellers", "cart", "login", "register",
        "account", "order-confirmation", "review", "privacy-policy", "modern-slavery-policy",
        "supply-chain-policy", "review-policy", "early-access", "unsubscribe", "returns-policy",
        "security", "terms-conditions", "cookie-policy", "accessibility-report", "compliance-report",
        "settings", "forgot-password", "reset-password", "verify-email", "blog", "designers", "style",
        "dresses", "collections", "top-picks", "care", "contact", "reviews", "about", "api",
    };

    /// <summary>True when <paramref name="path"/> is an address a section page can be created at.</summary>
    public static bool IsCreatablePagePath(string path)
    {
        if (!PagePath.IsMatch(path))
        {
            return false;
        }
        return !ReservedFirstSegments.Contains(FirstSegment(path));
    }

    private static string FirstSegment(string path) => path.Length > 1 ? path[1..].Split('/')[0] : "";

    public static IReadOnlyList<HomeSectionDto> Defaults { get; } =
    [
        new(
            HomeSectionKinds.LatestCollection,
            "Our Latest Collection",
            "Noir",
            "Dark, moody vintage pieces united by dramatic sleeves, from Jaeger and Laura Ashley to Polly Peck and Louis Féraud.",
            "Browse entire collection",
            "/collections/noir",
            true,
            []),
        new(
            HomeSectionKinds.TopPicks,
            "Our Top Picks",
            "Our Top Picks",
            "A rotating, hand-chosen selection of standout vintage from across the shop.",
            "See all top picks",
            "/top-picks",
            true,
            []),
    ];

    public async Task<List<HomeSectionDto>> GetPublicAsync()
    {
        List<HomeSectionDto> all = await GetAdminAsync();
        // Top Picks stays in regardless of Visible: its own switch governs it, applied by the frontend.
        return all.Where(s => s.Visible || s.Kind == HomeSectionKinds.TopPicks).ToList();
    }

    public async Task<SectionPageDto?> GetPageAsync(string path)
    {
        string wanted = (path ?? "").Trim().TrimEnd('/').ToLowerInvariant();
        if (!IsCreatablePagePath(wanted))
        {
            return null;
        }
        HomeSection? section = await sections.Query()
            .FirstOrDefaultAsync(s => s.CreatePage && s.Kind == HomeSectionKinds.Custom && s.ButtonLink == wanted);
        if (section is null)
        {
            return null;
        }
        return ToPage(ToDto(section));
    }

    public async Task<List<SectionPageDto>> GetPagesAsync()
    {
        List<HomeSection> rows = await sections.Query()
            .Where(s => s.CreatePage && s.Kind == HomeSectionKinds.Custom)
            .OrderBy(s => s.Position)
            .ToListAsync();
        return rows.Select(r => ToPage(ToDto(r))).ToList();
    }

    private static SectionPageDto ToPage(HomeSectionDto d) =>
        new(d.ButtonLink, d.Eyebrow, d.Title, d.Description, d.ProductIds);

    public async Task<List<HomeSectionDto>> GetAdminAsync()
    {
        List<HomeSection> rows = await sections.Query().OrderBy(s => s.Position).ToListAsync();
        if (rows.Count == 0)
        {
            return Defaults.ToList();
        }
        return rows.Select(ToDto).ToList();
    }

    public async Task<List<HomeSectionDto>> ReplaceAsync(IReadOnlyList<HomeSectionDto> submitted)
    {
        List<HomeSectionDto> clean = Validate(submitted);

        // Full replace, as with Top Picks: the removals and inserts commit in AddRangeAsync's
        // single SaveChanges.
        IEnumerable<HomeSection> existing = await sections.GetAllAsync();
        await sections.RemoveRangeAsync(existing);

        List<HomeSection> fresh = clean
            .Select((s, index) => new HomeSection
            {
                Kind = s.Kind,
                Eyebrow = s.Eyebrow,
                Title = s.Title,
                Description = s.Description,
                ButtonText = s.ButtonText,
                ButtonLink = s.ButtonLink,
                Position = index,
                Visible = s.Visible,
                ProductIds = s.ProductIds.Select(id => id.ToString()).ToList(),
                CreatePage = s.CreatePage,
            })
            .ToList();
        await sections.AddRangeAsync(fresh);

        return clean;
    }

    private static List<HomeSectionDto> Validate(IReadOnlyList<HomeSectionDto> submitted)
    {
        if (submitted.Count > MaxSections)
        {
            throw new HomeSectionValidationException($"The homepage can have at most {MaxSections} sections.");
        }

        List<HomeSectionDto> clean = [];
        HashSet<string> pagePaths = new(StringComparer.Ordinal);
        foreach (HomeSectionDto raw in submitted)
        {
            string kind = (raw.Kind ?? "").Trim().ToLowerInvariant();
            if (!HomeSectionKinds.All.Contains(kind))
            {
                throw new HomeSectionValidationException($"Unknown section type '{raw.Kind}'.");
            }

            string title = Trimmed(raw.Title, MaxTitle, "Title");
            if (title.Length == 0)
            {
                throw new HomeSectionValidationException("Every section needs a title.");
            }

            string buttonText = Trimmed(raw.ButtonText, MaxButtonText, "Button text");
            string buttonLink = Trimmed(raw.ButtonLink, MaxButtonLink, "Button link");
            // Site-relative only. An editable link that could point anywhere would let a compromised
            // or careless edit send shoppers off-site from the homepage; "//host" is protocol-relative.
            if (buttonLink.Length > 0 && (!buttonLink.StartsWith('/') || buttonLink.StartsWith("//") || buttonLink.Contains('\\')))
            {
                throw new HomeSectionValidationException(
                    $"The button link for \"{title}\" must be a page on this site, starting with / (for example /shop/sale).");
            }
            if (buttonLink.Length > 0 && buttonText.Length == 0)
            {
                throw new HomeSectionValidationException($"\"{title}\" has a button link but no button text.");
            }

            List<Guid> productIds = [];
            if (kind == HomeSectionKinds.Custom)
            {
                HashSet<Guid> seen = [];
                foreach (Guid id in raw.ProductIds ?? [])
                {
                    if (id != Guid.Empty && seen.Add(id))
                    {
                        productIds.Add(id);
                    }
                }
                if (productIds.Count > MaxProductsPerSection)
                {
                    throw new HomeSectionValidationException(
                        $"\"{title}\" has {productIds.Count} pieces; a section can show at most {MaxProductsPerSection}.");
                }
            }

            // A page is only created where the site has nothing of its own. At one of the site's
            // addresses (e.g. /shop/sale) the tick is dropped and the button links to that page.
            bool createPage = false;
            if (raw.CreatePage && kind == HomeSectionKinds.Custom)
            {
                if (buttonLink.Length == 0)
                {
                    throw new HomeSectionValidationException(
                        $"\"{title}\" needs a \"Button goes to\" address for its page, e.g. /autumn-knits.");
                }
                string normalised = buttonLink.TrimEnd('/').ToLowerInvariant();
                if (normalised.Length > 1 && !ReservedFirstSegments.Contains(FirstSegment(normalised)))
                {
                    if (!PagePath.IsMatch(normalised))
                    {
                        throw new HomeSectionValidationException(
                            $"The page address for \"{title}\" can only use lowercase letters, numbers and hyphens, " +
                            "in up to three parts, e.g. /autumn-knits or /edits/autumn-knits.");
                    }
                    if (!pagePaths.Add(normalised))
                    {
                        throw new HomeSectionValidationException(
                            $"Two sections both want to create the page {normalised}. Give one of them a different address.");
                    }
                    buttonLink = normalised;
                    createPage = true;
                }
            }

            clean.Add(new HomeSectionDto(
                kind,
                Trimmed(raw.Eyebrow, MaxEyebrow, "Eyebrow"),
                title,
                Trimmed(raw.Description, MaxDescription, "Description"),
                buttonText,
                buttonLink,
                raw.Visible,
                productIds,
                createPage));
        }

        // The built-in strips can be hidden, renamed and moved, but not deleted: their pieces come
        // from elsewhere (the Top Picks tab, the collection profile) and deleting one here would
        // silently orphan that curation.
        foreach (string builtIn in HomeSectionKinds.BuiltIn)
        {
            int count = clean.Count(s => s.Kind == builtIn);
            if (count != 1)
            {
                throw new HomeSectionValidationException(
                    count == 0
                        ? $"The '{builtIn}' section can be hidden but not removed."
                        : $"There can only be one '{builtIn}' section.");
            }
        }

        return clean;
    }

    private static string Trimmed(string? value, int max, string field)
    {
        string trimmed = (value ?? "").Trim();
        if (trimmed.Length > max)
        {
            throw new HomeSectionValidationException($"{field} can be at most {max} characters.");
        }
        return trimmed;
    }

    private static HomeSectionDto ToDto(HomeSection s) => new(
        s.Kind,
        s.Eyebrow,
        s.Title,
        s.Description,
        s.ButtonText,
        s.ButtonLink,
        s.Visible,
        s.ProductIds.Select(id => Guid.TryParse(id, out Guid g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList(),
        s.CreatePage);
}
