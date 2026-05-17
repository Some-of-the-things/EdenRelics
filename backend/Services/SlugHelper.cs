using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Eden_Relics_BE.Services;

public static partial class SlugHelper
{
    private const int MaxSlugLength = 80;

    public static string Generate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "";
        }

        string normalized = input.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        string stripped = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        string hyphenated = NonSlugChars().Replace(stripped, "-");
        string collapsed = MultipleHyphens().Replace(hyphenated, "-").Trim('-');

        return collapsed.Length > MaxSlugLength
            ? collapsed[..MaxSlugLength].TrimEnd('-')
            : collapsed;
    }

    /// <summary>Make slug unique by appending -2, -3 etc. until none of the supplied existing slugs match.</summary>
    public static string MakeUnique(string baseSlug, IEnumerable<string> existingSlugs)
    {
        HashSet<string> taken = new(existingSlugs, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }

        int suffix = 2;
        string candidate;
        do
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }
        while (taken.Contains(candidate));

        return candidate;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphens();
}
