using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
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

    public static IReadOnlyList<HomeSectionDto> Defaults { get; } =
    [
        new(
            HomeSectionKinds.TopPicks,
            "Our Top Picks",
            "Our Top Picks",
            "A rotating, hand-chosen selection of standout vintage from across the shop.",
            "See all top picks",
            "/top-picks",
            true,
            []),
        new(
            HomeSectionKinds.LatestCollection,
            "Our Latest Collection",
            "The Wildflower Edit",
            "Vintage dresses united by romantic florals, graceful silhouettes and thoughtful craftsmanship.",
            "Browse entire collection",
            "/collections/wildflower-edit",
            true,
            []),
    ];

    public async Task<List<HomeSectionDto>> GetPublicAsync()
    {
        List<HomeSectionDto> all = await GetAdminAsync();
        // Top Picks stays in regardless of Visible: its own switch governs it, applied by the frontend.
        return all.Where(s => s.Visible || s.Kind == HomeSectionKinds.TopPicks).ToList();
    }

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

            clean.Add(new HomeSectionDto(
                kind,
                Trimmed(raw.Eyebrow, MaxEyebrow, "Eyebrow"),
                title,
                Trimmed(raw.Description, MaxDescription, "Description"),
                buttonText,
                buttonLink,
                raw.Visible,
                productIds));
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
        s.ProductIds.Select(id => Guid.TryParse(id, out Guid g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList());
}
