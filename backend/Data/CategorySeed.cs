using System.Text.Json;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Data;

/// <summary>
/// Seeds the three launch hubs and the 15 sub-categories that used to be hard-coded in the
/// frontend (designers.data.ts / category.data.ts), with their text, slugs and match words
/// unchanged. Runs on every startup but only acts on an empty table (deleted rows count), so it
/// never re-creates a page an admin removed or overwrites an edit.
/// </summary>
public static class CategorySeed
{
    public static readonly (string Key, string Name)[] Hubs =
    [
        ("designers", "Vintage Designers & Labels"),
        ("style", "Shop by Style"),
        ("dresses", "Dress Types"),
    ];

    private sealed record SeedRow(
        string HubKey, string Slug, string Name, string Tagline, string Intro,
        string MetaTitle, string MetaDescription, int DisplayOrder,
        List<string> IncludeKeywords, List<string> ExcludeKeywords, string ContentKey);

    public static async Task EnsureSeedDataAsync(EdenRelicsDbContext db)
    {
        if (!await db.CategoryHubs.IgnoreQueryFilters().AnyAsync())
        {
            for (int i = 0; i < Hubs.Length; i++)
            {
                db.CategoryHubs.Add(new CategoryHub { Key = Hubs[i].Key, Name = Hubs[i].Name, DisplayOrder = i });
            }
            await db.SaveChangesAsync();
        }

        if (await db.SubCategories.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        foreach (SeedRow row in LoadRows())
        {
            db.SubCategories.Add(new SubCategory
            {
                HubKey = row.HubKey,
                Slug = row.Slug,
                Name = row.Name,
                Tagline = row.Tagline,
                Intro = row.Intro,
                MetaTitle = row.MetaTitle,
                MetaDescription = row.MetaDescription,
                DisplayOrder = row.DisplayOrder,
                IncludeKeywords = row.IncludeKeywords,
                ExcludeKeywords = row.ExcludeKeywords,
                ContentKey = row.ContentKey,
            });
        }
        await db.SaveChangesAsync();
    }

    private static List<SeedRow> LoadRows()
    {
        using Stream stream = typeof(CategorySeed).Assembly
            .GetManifestResourceStream("Eden_Relics_BE.Data.SubCategorySeed.json")
            ?? throw new InvalidOperationException("SubCategorySeed.json is not embedded in the assembly.");
        return JsonSerializer.Deserialize<List<SeedRow>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];
    }
}
