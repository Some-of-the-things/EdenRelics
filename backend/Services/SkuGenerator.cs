using System.Text.RegularExpressions;

namespace Eden_Relics_BE.Services;

public static partial class SkuGenerator
{
    public const string Prefix = "ER-";
    public const int MaxLength = 50;

    [GeneratedRegex(@"^ER-(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SequentialSkuRegex();

    /// <summary>
    /// Generate the next sequential SKU (e.g. ER-00001) given the set of existing SKUs.
    /// Caller is responsible for handling unique-violation retries if races occur.
    /// </summary>
    public static string Next(IEnumerable<string> existingSkus)
    {
        int max = 0;
        foreach (string sku in existingSkus)
        {
            Match match = SequentialSkuRegex().Match(sku);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int n) && n > max)
            {
                max = n;
            }
        }
        return $"{Prefix}{max + 1:D5}";
    }

    /// <summary>
    /// Validate a manually-entered SKU. Returns null if valid, otherwise an error message.
    /// </summary>
    public static string? Validate(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return "SKU cannot be empty.";
        }
        string trimmed = sku.Trim();
        if (trimmed.Length > MaxLength)
        {
            return $"SKU must be {MaxLength} characters or fewer.";
        }
        return null;
    }
}
