using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// First-party "vintage care" content: fabric and problem guides. All persistence via
/// repositories. Publishing is gated on expert approval — only <see cref="CareReviewStatus.ExpertApproved"/>
/// entries are ever served publicly or listed in the sitemap.
/// </summary>
public class CareService(
    IRepository<CareFabric> fabrics,
    IRepository<CareIssue> issues,
    IRepository<CareGuidance> guidances,
    IRepository<Product> products,
    ICareDraftService draftService) : ICareService
{
    public bool AiDraftingAvailable => draftService.IsConfigured;

    // --- Worklist ---

    public async Task<List<CareWorklistItemDto>> GetWorklistAsync()
    {
        List<CareFabric> fab = await fabrics.Query().ToListAsync();
        List<CareIssue> iss = await issues.Query().ToListAsync();

        IEnumerable<CareWorklistItemDto> items = fab
            .Select(f => new CareWorklistItemDto(
                f.Id, "fabric", f.Name, f.Slug, f.Status.ToString(), f.IsPublished,
                NeedsAction: !f.IsPublished, f.TargetKeywords, f.ReviewNotes, f.LastReviewedUtc, f.UpdatedAtUtc))
            .Concat(iss.Select(i => new CareWorklistItemDto(
                i.Id, "issue", i.Name, i.Slug, i.Status.ToString(), i.IsPublished,
                NeedsAction: !i.IsPublished, i.TargetKeywords, i.ReviewNotes, i.LastReviewedUtc, i.UpdatedAtUtc)));

        // Outstanding actions first, then most-recently-touched.
        return items
            .OrderByDescending(i => i.NeedsAction)
            .ThenByDescending(i => i.UpdatedAtUtc)
            .ToList();
    }

    // --- Fabric ---

    public async Task<CareFabricDto?> GetFabricAsync(Guid id)
    {
        CareFabric? f = await fabrics.GetByIdAsync(id);
        return f is null ? null : ToDto(f);
    }

    public async Task<CareFabricDto> CreateFabricAsync(SaveCareFabricDto dto)
    {
        CareFabric f = new()
        {
            Slug = await UniqueFabricSlugAsync(string.IsNullOrWhiteSpace(dto.Slug) ? dto.Name : dto.Slug!),
            Name = dto.Name,
            Status = CareReviewStatus.Draft,
        };
        Apply(f, dto);
        await fabrics.AddAsync(f);
        return ToDto(f);
    }

    public async Task<CareFabricDto?> UpdateFabricAsync(Guid id, SaveCareFabricDto dto)
    {
        CareFabric? f = await fabrics.GetByIdAsync(id);
        if (f is null)
        {
            return null;
        }

        Apply(f, dto);
        await fabrics.UpdateAsync(f);
        return ToDto(f);
    }

    public async Task<CareFabricDto?> SetFabricPublishedAsync(Guid id, bool published, string reviewedBy)
    {
        CareFabric? f = await fabrics.GetByIdAsync(id);
        if (f is null)
        {
            return null;
        }

        if (published)
        {
            f.Status = CareReviewStatus.ExpertApproved;
            f.IsPublished = true;
            f.ReviewedBy = reviewedBy;
            f.LastReviewedUtc = DateTime.UtcNow;
        }
        else
        {
            f.IsPublished = false;
        }

        await fabrics.UpdateAsync(f);
        return ToDto(f);
    }

    public async Task<CareFabricDto?> GenerateFabricDraftAsync(Guid id)
    {
        CareFabric? f = await fabrics.GetByIdAsync(id);
        if (f is null)
        {
            return null;
        }

        CareFabricDraft? draft = await draftService.DraftFabricAsync(f.Name, f.TargetKeywords);
        if (draft is null)
        {
            return ToDto(f);   // AI unavailable/failed — leave the entry untouched.
        }

        f.Intro = draft.Intro;
        f.FiberContent = draft.FiberContent;
        f.HowToIdentify = draft.HowToIdentify;
        f.Washing = draft.Washing;
        f.Drying = draft.Drying;
        f.Ironing = draft.Ironing;
        f.Storing = draft.Storing;
        f.VintageCautions = draft.VintageCautions;
        f.Dos = draft.Dos;
        f.Donts = draft.Donts;
        f.MetaTitle = draft.MetaTitle;
        f.MetaDescription = draft.MetaDescription;
        f.Status = CareReviewStatus.AiDrafted;   // drafted, never auto-published
        await fabrics.UpdateAsync(f);
        return ToDto(f);
    }

    public async Task<CareFabricDto?> GetPublishedFabricAsync(string slug)
    {
        CareFabric? f = await fabrics.Query()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished);
        return f is null ? null : ToDto(f);
    }

    // --- Issue ---

    public async Task<CareIssueDto?> GetIssueAsync(Guid id)
    {
        CareIssue? i = await issues.GetByIdAsync(id);
        return i is null ? null : ToDto(i);
    }

    public async Task<CareIssueDto> CreateIssueAsync(SaveCareIssueDto dto)
    {
        CareIssue i = new()
        {
            Slug = await UniqueIssueSlugAsync(string.IsNullOrWhiteSpace(dto.Slug) ? dto.Name : dto.Slug!),
            Name = dto.Name,
            Status = CareReviewStatus.Draft,
        };
        Apply(i, dto);
        await issues.AddAsync(i);
        return ToDto(i);
    }

    public async Task<CareIssueDto?> UpdateIssueAsync(Guid id, SaveCareIssueDto dto)
    {
        CareIssue? i = await issues.GetByIdAsync(id);
        if (i is null)
        {
            return null;
        }

        Apply(i, dto);
        await issues.UpdateAsync(i);
        return ToDto(i);
    }

    public async Task<CareIssueDto?> SetIssuePublishedAsync(Guid id, bool published, string reviewedBy)
    {
        CareIssue? i = await issues.GetByIdAsync(id);
        if (i is null)
        {
            return null;
        }

        if (published)
        {
            i.Status = CareReviewStatus.ExpertApproved;
            i.IsPublished = true;
            i.ReviewedBy = reviewedBy;
            i.LastReviewedUtc = DateTime.UtcNow;
        }
        else
        {
            i.IsPublished = false;
        }

        await issues.UpdateAsync(i);
        return ToDto(i);
    }

    public async Task<CareIssueDto?> GenerateIssueDraftAsync(Guid id)
    {
        CareIssue? i = await issues.GetByIdAsync(id);
        if (i is null)
        {
            return null;
        }

        CareIssueDraft? draft = await draftService.DraftIssueAsync(i.Name, i.TargetKeywords);
        if (draft is null)
        {
            return ToDto(i);
        }

        i.Causes = draft.Causes;
        i.GeneralMethod = draft.GeneralMethod;
        i.WhatNotToDo = draft.WhatNotToDo;
        i.WhenToSeeAPro = draft.WhenToSeeAPro;
        i.MetaTitle = draft.MetaTitle;
        i.MetaDescription = draft.MetaDescription;
        i.Status = CareReviewStatus.AiDrafted;
        await issues.UpdateAsync(i);
        return ToDto(i);
    }

    public async Task<CareIssueDto?> GetPublishedIssueAsync(string slug)
    {
        CareIssue? i = await issues.Query()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished);
        return i is null ? null : ToDto(i);
    }

    public async Task<CareIndexDto> GetPublishedIndexAsync()
    {
        List<CareFabric> f = await fabrics.Query()
            .Where(x => x.IsPublished).OrderBy(x => x.Name).ToListAsync();
        List<CareIssue> i = await issues.Query()
            .Where(x => x.IsPublished).OrderBy(x => x.Name).ToListAsync();

        return new CareIndexDto(
            f.Select(x => new CareIndexItemDto(x.Name, x.Slug, Summarise(x.MetaDescription, x.Intro))).ToList(),
            i.Select(x => new CareIndexItemDto(x.Name, x.Slug, Summarise(x.MetaDescription, x.Causes))).ToList());
    }

    private static string Summarise(string preferred, string fallback)
    {
        string s = (string.IsNullOrWhiteSpace(preferred) ? fallback : preferred).Trim();
        return s.Length > 160 ? s[..157].TrimEnd() + "…" : s;
    }

    // --- Inventory cross-linking ---

    public async Task<CareFabricRefDto?> ResolveFabricForMaterialAsync(string material)
    {
        if (string.IsNullOrWhiteSpace(material))
        {
            return null;
        }

        List<CareFabric> published = await fabrics.Query().Where(f => f.IsPublished).ToListAsync();
        CareFabric? match = published.FirstOrDefault(f => MaterialMatchesFabric(material, f));
        return match is null ? null : new CareFabricRefDto(match.Slug, match.Name);
    }

    public async Task<List<CareProductDto>> GetFabricProductsAsync(string slug)
    {
        CareFabric? fabric = await fabrics.Query().FirstOrDefaultAsync(f => f.Slug == slug && f.IsPublished);
        if (fabric is null)
        {
            return [];
        }

        List<Product> live = await products.Query()
            .Where(p => p.Status == ProductStatus.Live && p.Material != null && p.Material != "")
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        return live
            .Where(p => MaterialMatchesFabric(p.Material!, fabric))
            .Take(12)
            .Select(p => new CareProductDto(
                p.Id,
                p.Name,
                string.IsNullOrEmpty(p.Slug) ? p.Id.ToString() : p.Slug,
                p.Price,
                p.SalePrice,
                p.ImageUrl))
            .ToList();
    }

    // --- Interactive finder ---

    public async Task<CareFinderResultDto?> GetFinderResultAsync(string fabricSlug, string issueSlug)
    {
        CareFabric? fabric = await fabrics.Query().FirstOrDefaultAsync(f => f.Slug == fabricSlug && f.IsPublished);
        CareIssue? issue = await issues.Query().FirstOrDefaultAsync(i => i.Slug == issueSlug && i.IsPublished);
        if (fabric is null || issue is null)
        {
            return null;
        }

        CareGuidance? guidance = await guidances.Query()
            .FirstOrDefaultAsync(g => g.FabricId == fabric.Id && g.IssueId == issue.Id
                && g.Status == CareReviewStatus.ExpertApproved);

        if (guidance is not null)
        {
            return new CareFinderResultDto(
                fabric.Name, fabric.Slug, issue.Name, issue.Slug,
                guidance.Safety.ToString(), guidance.ShortAnswer, guidance.SpecificMethod, IsGeneral: false);
        }

        // No expert override yet — compose a general answer from the two approved guides.
        string method = ComposeFallback(fabric, issue);
        return new CareFinderResultDto(
            fabric.Name, fabric.Slug, issue.Name, issue.Slug,
            CareSafety.Unknown.ToString(), "", method, IsGeneral: true);
    }

    private static string ComposeFallback(CareFabric fabric, CareIssue issue)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(issue.GeneralMethod))
        {
            parts.Add(issue.GeneralMethod.Trim());
        }
        string fabricNote = !string.IsNullOrWhiteSpace(fabric.VintageCautions)
            ? fabric.VintageCautions.Trim()
            : fabric.Washing.Trim();
        if (!string.IsNullOrWhiteSpace(fabricNote))
        {
            parts.Add($"For vintage {fabric.Name} specifically: {fabricNote}");
        }
        parts.Add("This is general guidance — always spot-test on a hidden seam first, and when a piece is fragile or valuable, choose a specialist cleaner over any home method.");
        return string.Join("\n\n", parts);
    }

    public async Task<CareGuidanceDto?> GetGuidanceAsync(Guid fabricId, Guid issueId)
    {
        CareGuidance? g = await guidances.Query()
            .FirstOrDefaultAsync(x => x.FabricId == fabricId && x.IssueId == issueId);
        return g is null ? null : ToDto(g);
    }

    public async Task<CareGuidanceDto?> SaveGuidanceAsync(SaveCareGuidanceDto dto)
    {
        bool fabricExists = await fabrics.Query().AnyAsync(f => f.Id == dto.FabricId);
        bool issueExists = await issues.Query().AnyAsync(i => i.Id == dto.IssueId);
        if (!fabricExists || !issueExists)
        {
            return null;
        }

        CareSafety safety = Enum.TryParse(dto.Safety, ignoreCase: true, out CareSafety s) ? s : CareSafety.Unknown;
        CareReviewStatus status = dto.Approved ? CareReviewStatus.ExpertApproved : CareReviewStatus.Draft;

        CareGuidance? existing = await guidances.Query()
            .FirstOrDefaultAsync(x => x.FabricId == dto.FabricId && x.IssueId == dto.IssueId);

        if (existing is null)
        {
            CareGuidance created = new()
            {
                FabricId = dto.FabricId,
                IssueId = dto.IssueId,
                Safety = safety,
                ShortAnswer = dto.ShortAnswer ?? "",
                SpecificMethod = dto.SpecificMethod ?? "",
                Status = status,
            };
            await guidances.AddAsync(created);
            return ToDto(created);
        }

        existing.Safety = safety;
        existing.ShortAnswer = dto.ShortAnswer ?? "";
        existing.SpecificMethod = dto.SpecificMethod ?? "";
        existing.Status = status;
        await guidances.UpdateAsync(existing);
        return ToDto(existing);
    }

    private static CareGuidanceDto ToDto(CareGuidance g) => new(
        g.Id, g.FabricId, g.IssueId, g.Safety.ToString(), g.ShortAnswer, g.SpecificMethod, g.Status.ToString());

    // --- Image-based fabric identification ---

    public async Task<CareIdentifyResultDto> IdentifyFabricAsync(string base64Image, string mediaType)
    {
        List<CareFabric> published = await fabrics.Query().Where(f => f.IsPublished).ToListAsync();
        FabricIdentifyResult? raw = await draftService.IdentifyFabricAsync(
            base64Image, mediaType, published.Select(f => f.Name).ToList());

        if (raw is null)
        {
            return new CareIdentifyResultDto(
                [], "Sorry — we couldn't analyse that image. Try a clearer, well-lit close-up of the fabric.");
        }

        List<CareIdentifyGuessDto> guesses = (raw.Guesses ?? [])
            .Where(g => !string.IsNullOrWhiteSpace(g.Name))
            .Select(g =>
            {
                CareFabric? match = published.FirstOrDefault(f => MaterialMatchesFabric(g.Name, f));
                return new CareIdentifyGuessDto(g.Name.Trim(), Math.Clamp(g.Confidence, 0, 1), match?.Slug);
            })
            .ToList();

        return new CareIdentifyResultDto(guesses, raw.Note ?? "");
    }

    /// <summary>A product's free-text material maps to a fabric guide by name, alias, or slug.</summary>
    private static bool MaterialMatchesFabric(string material, CareFabric fabric)
    {
        string m = material.Trim();
        if (string.Equals(m, fabric.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (fabric.AlsoKnownAs.Any(a => string.Equals(a, m, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return SlugHelper.Generate(m) == fabric.Slug;
    }

    // --- Mapping ---

    private static void Apply(CareFabric f, SaveCareFabricDto dto)
    {
        f.Name = dto.Name;
        f.AlsoKnownAs = dto.AlsoKnownAs ?? [];
        f.TargetKeywords = dto.TargetKeywords ?? [];
        f.Intro = dto.Intro ?? "";
        f.FiberContent = dto.FiberContent ?? "";
        f.HowToIdentify = dto.HowToIdentify ?? "";
        f.Washing = dto.Washing ?? "";
        f.Drying = dto.Drying ?? "";
        f.Ironing = dto.Ironing ?? "";
        f.Storing = dto.Storing ?? "";
        f.VintageCautions = dto.VintageCautions ?? "";
        f.Dos = dto.Dos ?? [];
        f.Donts = dto.Donts ?? [];
        f.MetaTitle = dto.MetaTitle ?? "";
        f.MetaDescription = dto.MetaDescription ?? "";
        f.ReviewNotes = dto.ReviewNotes ?? "";
    }

    private static void Apply(CareIssue i, SaveCareIssueDto dto)
    {
        i.Name = dto.Name;
        i.AlsoKnownAs = dto.AlsoKnownAs ?? [];
        i.TargetKeywords = dto.TargetKeywords ?? [];
        i.Causes = dto.Causes ?? "";
        i.GeneralMethod = dto.GeneralMethod ?? "";
        i.WhatNotToDo = dto.WhatNotToDo ?? "";
        i.WhenToSeeAPro = dto.WhenToSeeAPro ?? "";
        i.MetaTitle = dto.MetaTitle ?? "";
        i.MetaDescription = dto.MetaDescription ?? "";
        i.ReviewNotes = dto.ReviewNotes ?? "";
    }

    private static CareFabricDto ToDto(CareFabric f) => new(
        f.Id, f.Slug, f.Name, f.AlsoKnownAs, f.TargetKeywords, f.Intro, f.FiberContent, f.HowToIdentify,
        f.Washing, f.Drying, f.Ironing, f.Storing, f.VintageCautions, f.Dos, f.Donts,
        f.MetaTitle, f.MetaDescription, f.Status.ToString(), f.ReviewNotes, f.ReviewedBy,
        f.LastReviewedUtc, f.IsPublished, f.UpdatedAtUtc);

    private static CareIssueDto ToDto(CareIssue i) => new(
        i.Id, i.Slug, i.Name, i.AlsoKnownAs, i.TargetKeywords, i.Causes, i.GeneralMethod, i.WhatNotToDo,
        i.WhenToSeeAPro, i.MetaTitle, i.MetaDescription, i.Status.ToString(), i.ReviewNotes, i.ReviewedBy,
        i.LastReviewedUtc, i.IsPublished, i.UpdatedAtUtc);

    // --- Slugs (unique across soft-deleted rows too, since the index ignores IsDeleted) ---

    private async Task<string> UniqueFabricSlugAsync(string source)
    {
        string base_ = SlugBase(source);
        string candidate = base_;
        int n = 2;
        while (await fabrics.Query(includeDeleted: true).AnyAsync(f => f.Slug == candidate))
        {
            candidate = $"{base_}-{n++}";
        }
        return candidate;
    }

    private async Task<string> UniqueIssueSlugAsync(string source)
    {
        string base_ = SlugBase(source);
        string candidate = base_;
        int n = 2;
        while (await issues.Query(includeDeleted: true).AnyAsync(i => i.Slug == candidate))
        {
            candidate = $"{base_}-{n++}";
        }
        return candidate;
    }

    private static string SlugBase(string source)
    {
        string slug = SlugHelper.Generate(source);
        return string.IsNullOrEmpty(slug) ? "entry" : slug;
    }
}
