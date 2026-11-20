using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class CategoryValidationException(string message) : Exception(message);

public interface ICategoryService
{
    Task<CategoriesDto> GetPublicAsync();
    Task<CategoriesDto> GetAdminAsync();
    Task<SubCategoryDto> CreateAsync(SaveSubCategoryRequest request);
    Task<SubCategoryDto?> UpdateAsync(Guid id, SaveSubCategoryRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task ReorderAsync(string hubKey, List<Guid> ids);
    Task<SubCategoryDto?> SetProductsAsync(Guid subCategoryId, List<Guid> add, List<Guid> remove);
    Task<bool> SetProductCategoriesAsync(Guid productId, List<Guid> subCategoryIds);
}

/// <summary>
/// Sub-categories under the /designers, /style and /dresses hubs, edited from the admin.
///
/// Membership is keyword-first: a piece is in a sub-category when its name contains an include
/// word and no exclude word. Admins can override that per piece, in either direction; an override
/// is stored only where it differs from what the keywords say, so renaming a piece or editing the
/// words keeps behaving predictably. The frontend applies the same rule to live stock to show the
/// pages and counts.
/// </summary>
public partial class CategoryService(
    IRepository<CategoryHub> hubs,
    IRepository<SubCategory> subCategories,
    IRepository<SubCategoryProduct> overrides,
    IRepository<CategoryRedirect> redirects,
    IRepository<Product> products) : ICategoryService
{
    private const int MaxKeywords = 30;
    private const int MaxKeywordLength = 60;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    /// <summary>The same rule as the frontend's matcher: include hit, no exclude hit, case-insensitive.</summary>
    public static bool KeywordMatches(string productName, IEnumerable<string> include, IEnumerable<string> exclude)
    {
        string name = productName.ToLowerInvariant();
        if (exclude.Any(x => x.Length > 0 && name.Contains(x.ToLowerInvariant())))
        {
            return false;
        }
        return include.Any(k => k.Length > 0 && name.Contains(k.ToLowerInvariant()));
    }

    public static string HubPath(string hubKey) => $"/{hubKey}";

    public static string SubCategoryPath(string hubKey, string slug) => $"/{hubKey}/{slug}";

    public Task<CategoriesDto> GetPublicAsync() => BuildAsync(includeHidden: false);

    public Task<CategoriesDto> GetAdminAsync() => BuildAsync(includeHidden: true);

    private async Task<CategoriesDto> BuildAsync(bool includeHidden)
    {
        List<CategoryHub> hubRows = await hubs.Query().OrderBy(h => h.DisplayOrder).ToListAsync();
        List<SubCategory> subs = await subCategories.Query()
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync();
        List<SubCategoryProduct> overrideRows = await overrides.Query().ToListAsync();
        ILookup<Guid, SubCategoryProduct> bySub = overrideRows.ToLookup(o => o.SubCategoryId);

        List<CategoryHubDto> hubDtos = hubRows.Select(h => new CategoryHubDto(
            h.Key,
            h.Name,
            subs.Where(s => s.HubKey == h.Key && (includeHidden || s.IsVisible))
                .Select(s => ToDto(s, bySub[s.Id]))
                .ToList()))
            .ToList();

        List<CategoryRedirectDto> redirectDtos = (await redirects.Query().ToListAsync())
            .Select(r => new CategoryRedirectDto(r.HubKey, r.FromSlug, r.ToPath))
            .ToList();

        List<HiddenSubCategoryDto> hidden = subs
            .Where(s => !s.IsVisible)
            .Select(s => new HiddenSubCategoryDto(s.HubKey, s.Slug))
            .ToList();

        return new CategoriesDto(hubDtos, redirectDtos, hidden);
    }

    private static SubCategoryDto ToDto(SubCategory s, IEnumerable<SubCategoryProduct> rows)
    {
        List<SubCategoryProduct> list = rows.ToList();
        return new SubCategoryDto(
            s.Id, s.HubKey, s.Slug, s.Name, s.Tagline, s.Intro, s.Body, s.MetaTitle, s.MetaDescription,
            s.DisplayOrder, s.IsVisible, s.IncludeKeywords, s.ExcludeKeywords, s.ContentKey,
            list.Where(o => o.Included).Select(o => o.ProductId).ToList(),
            list.Where(o => !o.Included).Select(o => o.ProductId).ToList());
    }

    public async Task<SubCategoryDto> CreateAsync(SaveSubCategoryRequest request)
    {
        string hubKey = (request.HubKey ?? "").Trim();
        if (!await hubs.Query().AnyAsync(h => h.Key == hubKey))
        {
            throw new CategoryValidationException("Choose which hub this sub-category belongs to.");
        }

        SubCategory sub = new() { HubKey = hubKey, Slug = "", Name = "" };
        await ApplyAsync(sub, request, isNew: true);
        int nextOrder = await subCategories.Query().Where(s => s.HubKey == hubKey)
            .Select(s => (int?)s.DisplayOrder).MaxAsync() ?? -1;
        sub.DisplayOrder = nextOrder + 1;

        // A new page takes over its URL, so any redirect that was sending it elsewhere goes.
        await RemoveRedirectFromAsync(hubKey, sub.Slug);
        await subCategories.AddAsync(sub);
        return ToDto(sub, []);
    }

    public async Task<SubCategoryDto?> UpdateAsync(Guid id, SaveSubCategoryRequest request)
    {
        SubCategory? sub = await subCategories.Query().FirstOrDefaultAsync(s => s.Id == id);
        if (sub is null)
        {
            return null;
        }

        string oldSlug = sub.Slug;
        await ApplyAsync(sub, request, isNew: false);

        if (sub.Slug != oldSlug)
        {
            string newPath = SubCategoryPath(sub.HubKey, sub.Slug);
            await RemoveRedirectFromAsync(sub.HubKey, sub.Slug);
            await RepointRedirectsAsync(SubCategoryPath(sub.HubKey, oldSlug), newPath);
            await UpsertRedirectAsync(sub.HubKey, oldSlug, newPath);
        }

        await subCategories.UpdateAsync(sub);
        List<SubCategoryProduct> rows = await overrides.Query().Where(o => o.SubCategoryId == sub.Id).ToListAsync();
        return ToDto(sub, rows);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        SubCategory? sub = await subCategories.Query().FirstOrDefaultAsync(s => s.Id == id);
        if (sub is null)
        {
            return false;
        }

        string hubPath = HubPath(sub.HubKey);
        await RepointRedirectsAsync(SubCategoryPath(sub.HubKey, sub.Slug), hubPath);
        await UpsertRedirectAsync(sub.HubKey, sub.Slug, hubPath);

        List<SubCategoryProduct> rows = await overrides.Query().Where(o => o.SubCategoryId == sub.Id).ToListAsync();
        if (rows.Count > 0)
        {
            await overrides.RemoveRangeAsync(rows);
        }
        await subCategories.DeleteAsync(sub.Id);
        return true;
    }

    public async Task ReorderAsync(string hubKey, List<Guid> ids)
    {
        List<SubCategory> subs = await subCategories.Query().Where(s => s.HubKey == hubKey).ToListAsync();
        // Listed ids first, in the order given; anything not listed keeps its relative order after them.
        List<SubCategory> ordered = ids
            .Select(id => subs.FirstOrDefault(s => s.Id == id))
            .OfType<SubCategory>()
            .Distinct()
            .Concat(subs.Where(s => !ids.Contains(s.Id)).OrderBy(s => s.DisplayOrder))
            .ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].DisplayOrder = i;
        }
        await subCategories.AddRangeAsync([]);
    }

    public async Task<SubCategoryDto?> SetProductsAsync(Guid subCategoryId, List<Guid> add, List<Guid> remove)
    {
        SubCategory? sub = await subCategories.Query().FirstOrDefaultAsync(s => s.Id == subCategoryId);
        if (sub is null)
        {
            return null;
        }

        List<Guid> ids = add.Concat(remove).Distinct().ToList();
        Dictionary<Guid, Product> byId = await products.Query()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        List<SubCategoryProduct> rows = await overrides.Query()
            .Where(o => o.SubCategoryId == sub.Id)
            .ToListAsync();

        List<SubCategoryProduct> toRemove = [];
        List<SubCategoryProduct> toAdd = [];
        foreach ((Guid productId, bool member) in add.Select(id => (id, true)).Concat(remove.Select(id => (id, false))))
        {
            if (!byId.TryGetValue(productId, out Product? product))
            {
                continue;
            }
            ApplyMembership(sub, product, member, rows, toAdd, toRemove);
        }

        await CommitOverridesAsync(toAdd, toRemove);
        List<SubCategoryProduct> after = await overrides.Query().Where(o => o.SubCategoryId == sub.Id).ToListAsync();
        return ToDto(sub, after);
    }

    public async Task<bool> SetProductCategoriesAsync(Guid productId, List<Guid> subCategoryIds)
    {
        Product? product = await products.Query().FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null)
        {
            return false;
        }

        HashSet<Guid> wanted = [.. subCategoryIds];
        List<SubCategory> subs = await subCategories.Query().ToListAsync();
        List<SubCategoryProduct> rows = await overrides.Query().Where(o => o.ProductId == productId).ToListAsync();

        List<SubCategoryProduct> toRemove = [];
        List<SubCategoryProduct> toAdd = [];
        foreach (SubCategory sub in subs)
        {
            ApplyMembership(sub, product, wanted.Contains(sub.Id), rows, toAdd, toRemove);
        }

        await CommitOverridesAsync(toAdd, toRemove);
        return true;
    }

    /// <summary>
    /// Makes <paramref name="product"/>'s membership of <paramref name="sub"/> equal
    /// <paramref name="member"/>, storing an override only where the keywords would say otherwise.
    /// </summary>
    private static void ApplyMembership(
        SubCategory sub,
        Product product,
        bool member,
        List<SubCategoryProduct> existing,
        List<SubCategoryProduct> toAdd,
        List<SubCategoryProduct> toRemove)
    {
        bool byKeyword = KeywordMatches(product.Name, sub.IncludeKeywords, sub.ExcludeKeywords);
        SubCategoryProduct? row = existing.FirstOrDefault(o => o.SubCategoryId == sub.Id && o.ProductId == product.Id);

        if (member == byKeyword)
        {
            if (row is not null && !toRemove.Contains(row))
            {
                toRemove.Add(row);
                existing.Remove(row);
            }
            return;
        }

        if (row is not null)
        {
            row.Included = member;
            return;
        }

        SubCategoryProduct added = new() { SubCategoryId = sub.Id, ProductId = product.Id, Included = member };
        existing.Add(added);
        toAdd.Add(added);
    }

    private async Task CommitOverridesAsync(List<SubCategoryProduct> toAdd, List<SubCategoryProduct> toRemove)
    {
        if (toRemove.Count > 0)
        {
            await overrides.RemoveRangeAsync(toRemove);
        }
        // Also flushes any Included flips on rows that were already tracked.
        await overrides.AddRangeAsync(toAdd);
    }

    private async Task ApplyAsync(SubCategory sub, SaveSubCategoryRequest request, bool isNew)
    {
        string name = (request.Name ?? "").Trim();
        if (name.Length == 0)
        {
            throw new CategoryValidationException("Give the sub-category a name.");
        }
        if (name.Length > 120)
        {
            throw new CategoryValidationException("The name must be 120 characters or fewer.");
        }

        string slug = (request.Slug ?? "").Trim().ToLowerInvariant();
        if (slug.Length == 0)
        {
            slug = SlugHelper.Generate(name);
        }
        if (slug.Length == 0 || slug.Length > 80 || !SlugPattern().IsMatch(slug))
        {
            throw new CategoryValidationException(
                "The URL slug can only use lower-case letters, numbers and single hyphens (e.g. \"whimsigoth\").");
        }
        bool taken = await subCategories.Query()
            .AnyAsync(s => s.HubKey == sub.HubKey && s.Slug == slug && (isNew || s.Id != sub.Id));
        if (taken)
        {
            throw new CategoryValidationException($"Another page under /{sub.HubKey} already uses the slug \"{slug}\".");
        }

        sub.Name = name;
        sub.Slug = slug;
        sub.Tagline = Limit(request.Tagline, 300, "short line");
        sub.Intro = Limit(request.Intro, 2000, "short description");
        sub.Body = Limit(request.Body, 20000, "page text");
        sub.MetaTitle = Limit(request.MetaTitle, 200, "SEO title");
        sub.MetaDescription = Limit(request.MetaDescription, 400, "meta description");
        sub.IsVisible = request.IsVisible;
        sub.IncludeKeywords = CleanKeywords(request.IncludeKeywords);
        sub.ExcludeKeywords = CleanKeywords(request.ExcludeKeywords);
    }

    private static string Limit(string? value, int max, string label)
    {
        string trimmed = (value ?? "").Trim();
        if (trimmed.Length > max)
        {
            throw new CategoryValidationException($"The {label} must be {max} characters or fewer.");
        }
        return trimmed;
    }

    private static List<string> CleanKeywords(List<string>? words)
    {
        List<string> cleaned = (words ?? [])
            .Select(w => (w ?? "").Trim().ToLowerInvariant())
            .Where(w => w.Length > 0)
            .Distinct()
            .ToList();
        if (cleaned.Count > MaxKeywords)
        {
            throw new CategoryValidationException($"Use at most {MaxKeywords} match words.");
        }
        if (cleaned.Any(w => w.Length > MaxKeywordLength))
        {
            throw new CategoryValidationException($"Each match word must be {MaxKeywordLength} characters or fewer.");
        }
        return cleaned;
    }

    private async Task RemoveRedirectFromAsync(string hubKey, string slug)
    {
        List<CategoryRedirect> rows = await redirects.Query()
            .Where(r => r.HubKey == hubKey && r.FromSlug == slug)
            .ToListAsync();
        if (rows.Count > 0)
        {
            await redirects.RemoveRangeAsync(rows);
        }
    }

    /// <summary>Points redirects that ended at <paramref name="fromPath"/> straight at the new target, so there are no chains.</summary>
    private async Task RepointRedirectsAsync(string fromPath, string toPath)
    {
        List<CategoryRedirect> rows = await redirects.Query().Where(r => r.ToPath == fromPath).ToListAsync();
        foreach (CategoryRedirect row in rows)
        {
            row.ToPath = toPath;
        }
        if (rows.Count > 0)
        {
            await redirects.AddRangeAsync([]);
        }
    }

    private async Task UpsertRedirectAsync(string hubKey, string fromSlug, string toPath)
    {
        CategoryRedirect? row = await redirects.Query()
            .FirstOrDefaultAsync(r => r.HubKey == hubKey && r.FromSlug == fromSlug);
        if (row is null)
        {
            await redirects.AddAsync(new CategoryRedirect { HubKey = hubKey, FromSlug = fromSlug, ToPath = toPath });
            return;
        }
        row.ToPath = toPath;
        await redirects.UpdateAsync(row);
    }
}
