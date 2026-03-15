using System.Text.RegularExpressions;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public partial class SeoController(IHttpClientFactory httpClientFactory, EdenRelicsDbContext context, RankCheckerService rankChecker) : ControllerBase
{
    [HttpPost("analyse")]
    public async Task<ActionResult<SeoAnalysisResult>> Analyse([FromBody] SeoAnalyseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { message = "URL is required." });
        }

        HttpClient client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EdenRelics-SEO-Analyser/1.0");

        string html;
        try
        {
            html = await client.GetStringAsync(request.Url);
        }
        catch
        {
            return BadRequest(new { message = "Could not fetch the URL." });
        }

        string? title = ExtractFirst(TitleRegex(), html);
        string? metaDescription = ExtractMetaContent(html, "description");
        string? metaKeywords = ExtractMetaContent(html, "keywords");
        string? canonical = ExtractCanonical(html);
        string? ogTitle = ExtractMetaProperty(html, "og:title");
        string? ogDescription = ExtractMetaProperty(html, "og:description");
        string? ogImage = ExtractMetaProperty(html, "og:image");

        List<HeadingInfo> headings = ExtractHeadings(html);
        int imageCount = ImgRegex().Matches(html).Count;
        int imagesWithAlt = ImgWithAltRegex().Matches(html).Count;
        int imagesMissingAlt = imageCount - imagesWithAlt;

        string bodyText = StripHtml(html);
        int wordCount = WordRegex().Matches(bodyText).Count;

        int internalLinks = 0;
        int externalLinks = 0;
        foreach (Match m in AnchorHrefRegex().Matches(html))
        {
            string href = m.Groups[1].Value;
            if (href.StartsWith('#') || href.StartsWith("javascript", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (href.StartsWith('/') || href.Contains("edenrelics.co.uk"))
            {
                internalLinks++;
            }
            else if (href.StartsWith("http"))
            {
                externalLinks++;
            }
        }

        List<string> issues = [];
        List<string> warnings = [];
        List<string> passed = [];

        // Title
        if (string.IsNullOrWhiteSpace(title))
        {
            issues.Add("Missing <title> tag.");
        }
        else if (title.Length < 30)
        {
            warnings.Add($"Title is short ({title.Length} chars). Aim for 50-60.");
        }
        else if (title.Length > 60)
        {
            warnings.Add($"Title is long ({title.Length} chars). Aim for 50-60.");
        }
        else
        {
            passed.Add($"Title length is good ({title.Length} chars).");
        }

        // Meta description
        if (string.IsNullOrWhiteSpace(metaDescription))
        {
            issues.Add("Missing meta description.");
        }
        else if (metaDescription.Length < 120)
        {
            warnings.Add($"Meta description is short ({metaDescription.Length} chars). Aim for 150-160.");
        }
        else if (metaDescription.Length > 160)
        {
            warnings.Add($"Meta description is long ({metaDescription.Length} chars). Aim for 150-160.");
        }
        else
        {
            passed.Add($"Meta description length is good ({metaDescription.Length} chars).");
        }

        // H1
        int h1Count = headings.Count(h => h.Level == 1);
        if (h1Count == 0)
        {
            issues.Add("No H1 tag found.");
        }
        else if (h1Count > 1)
        {
            warnings.Add($"Multiple H1 tags found ({h1Count}). Use only one.");
        }
        else
        {
            passed.Add("Single H1 tag present.");
        }

        // Images
        if (imagesMissingAlt > 0)
        {
            warnings.Add($"{imagesMissingAlt} image(s) missing alt text.");
        }
        else if (imageCount > 0)
        {
            passed.Add($"All {imageCount} images have alt text.");
        }

        // Word count
        if (wordCount < 300)
        {
            warnings.Add($"Low word count ({wordCount}). Aim for 300+ words.");
        }
        else
        {
            passed.Add($"Word count is good ({wordCount} words).");
        }

        // Open Graph
        if (string.IsNullOrWhiteSpace(ogTitle))
        {
            warnings.Add("Missing og:title meta tag.");
        }
        if (string.IsNullOrWhiteSpace(ogDescription))
        {
            warnings.Add("Missing og:description meta tag.");
        }
        if (string.IsNullOrWhiteSpace(ogImage))
        {
            warnings.Add("Missing og:image meta tag.");
        }

        // Canonical
        if (string.IsNullOrWhiteSpace(canonical))
        {
            warnings.Add("Missing canonical link.");
        }
        else
        {
            passed.Add("Canonical URL present.");
        }

        List<KeywordSuggestion> suggestedKeywords = ExtractKeywords(html, title, metaDescription, headings);

        return Ok(new SeoAnalysisResult(
            request.Url,
            title,
            metaDescription,
            metaKeywords,
            canonical,
            new OpenGraphInfo(ogTitle, ogDescription, ogImage),
            headings,
            wordCount,
            imageCount,
            imagesMissingAlt,
            internalLinks,
            externalLinks,
            issues,
            warnings,
            passed,
            suggestedKeywords
        ));
    }

    private static string? ExtractFirst(Regex regex, string html)
    {
        Match m = regex.Match(html);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractMetaContent(string html, string name)
    {
        Match m = Regex.Match(html, $"""<meta\s+name=["']{name}["']\s+content=["']([^"']*)["']""",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }
        m = Regex.Match(html, $"""<meta\s+content=["']([^"']*)["']\s+name=["']{name}["']""",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractMetaProperty(string html, string property)
    {
        Match m = Regex.Match(html, $"""<meta\s+property=["']{Regex.Escape(property)}["']\s+content=["']([^"']*)["']""",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }
        m = Regex.Match(html, $"""<meta\s+content=["']([^"']*)["']\s+property=["']{Regex.Escape(property)}["']""",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractCanonical(string html)
    {
        Match m = Regex.Match(html, """<link\s+rel=["']canonical["']\s+href=["']([^"']*)["']""",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static List<HeadingInfo> ExtractHeadings(string html)
    {
        List<HeadingInfo> result = [];
        foreach (Match m in Regex.Matches(html, @"<h([1-6])[^>]*>(.*?)</h\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string text = Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim();
            result.Add(new HeadingInfo(int.Parse(m.Groups[1].Value), text));
        }
        return result;
    }

    private static string StripHtml(string html)
    {
        string noScript = Regex.Replace(html, @"<(script|style|noscript)[^>]*>.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return Regex.Replace(noScript, "<[^>]+>", " ");
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<img\b", RegexOptions.IgnoreCase)]
    private static partial Regex ImgRegex();

    [GeneratedRegex(@"<img\b[^>]*\balt\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ImgWithAltRegex();

    [GeneratedRegex(@"\b\w+\b")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"<a\b[^>]*\bhref\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorHrefRegex();

    // --- Keyword extraction ---

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by",
        "from", "as", "is", "was", "are", "were", "been", "be", "have", "has", "had", "do", "does",
        "did", "will", "would", "could", "should", "may", "might", "shall", "can", "need", "must",
        "it", "its", "this", "that", "these", "those", "i", "you", "he", "she", "we", "they", "me",
        "him", "her", "us", "them", "my", "your", "his", "our", "their", "not", "no", "nor", "so",
        "if", "then", "than", "too", "very", "just", "about", "above", "after", "again", "all",
        "also", "am", "any", "because", "before", "between", "both", "each", "few", "get", "got",
        "here", "how", "into", "more", "most", "new", "now", "only", "other", "out", "own", "same",
        "some", "such", "there", "through", "up", "what", "when", "where", "which", "while", "who",
        "why", "down", "off", "over", "under", "until", "during", "www", "http", "https", "com"
    ];

    private static List<KeywordSuggestion> ExtractKeywords(string html, string? title, string? metaDescription, List<HeadingInfo> headings)
    {
        string bodyText = StripHtml(html).ToLowerInvariant();
        string[] words = AlphaWordRegex().Matches(bodyText)
            .Select(m => m.Value)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .ToArray();

        if (words.Length == 0)
        {
            return [];
        }

        // Score single words
        Dictionary<string, double> scores = [];
        foreach (string word in words)
        {
            scores.TryGetValue(word, out double current);
            scores[word] = current + 1;
        }

        // Score 2-word phrases
        for (int i = 0; i < words.Length - 1; i++)
        {
            string phrase = $"{words[i]} {words[i + 1]}";
            scores.TryGetValue(phrase, out double current);
            scores[phrase] = current + 2; // Phrases get more weight per occurrence
        }

        // Score 3-word phrases
        for (int i = 0; i < words.Length - 2; i++)
        {
            string phrase = $"{words[i]} {words[i + 1]} {words[i + 2]}";
            scores.TryGetValue(phrase, out double current);
            scores[phrase] = current + 3;
        }

        // Boost terms found in title
        if (!string.IsNullOrWhiteSpace(title))
        {
            string lowerTitle = title.ToLowerInvariant();
            foreach (string key in scores.Keys.ToArray())
            {
                if (lowerTitle.Contains(key))
                {
                    scores[key] *= 3;
                }
            }
        }

        // Boost terms found in meta description
        if (!string.IsNullOrWhiteSpace(metaDescription))
        {
            string lowerDesc = metaDescription.ToLowerInvariant();
            foreach (string key in scores.Keys.ToArray())
            {
                if (lowerDesc.Contains(key))
                {
                    scores[key] *= 2;
                }
            }
        }

        // Boost terms found in headings
        string headingText = string.Join(" ", headings.Select(h => h.Text)).ToLowerInvariant();
        foreach (string key in scores.Keys.ToArray())
        {
            if (headingText.Contains(key))
            {
                scores[key] *= 2;
            }
        }

        // Filter out single words that appear fewer than 2 times, keep phrases with higher scores
        return scores
            .Where(kv => kv.Value >= 4)
            .OrderByDescending(kv => kv.Value)
            .Take(20)
            .Select(kv =>
            {
                int frequency = words.Count(w => w == kv.Key);
                if (kv.Key.Contains(' '))
                {
                    // Count phrase frequency in original text
                    frequency = Regex.Matches(bodyText, Regex.Escape(kv.Key)).Count;
                }
                return new KeywordSuggestion(kv.Key, Math.Round(kv.Value, 1), frequency);
            })
            .ToList();
    }

    [GeneratedRegex(@"\b[a-z]+\b")]
    private static partial Regex AlphaWordRegex();

    // --- Tracked keywords CRUD ---

    [HttpGet("keywords")]
    public async Task<ActionResult<List<TrackedKeywordDto>>> GetTrackedKeywords()
    {
        List<TrackedKeyword> keywords = await context.TrackedKeywords
            .OrderByDescending(k => k.UpdatedAtUtc)
            .ToListAsync();
        return Ok(keywords.Select(ToDto).ToList());
    }

    [HttpPost("keywords")]
    public async Task<ActionResult<TrackedKeywordDto>> AddKeyword([FromBody] CreateTrackedKeywordDto dto)
    {
        int? position = dto.Position ?? await rankChecker.CheckRankAsync(dto.Keyword);

        TrackedKeyword keyword = new()
        {
            Keyword = dto.Keyword,
            PageUrl = dto.PageUrl,
            LastPosition = position,
            LastCheckedUtc = DateTime.UtcNow,
            Notes = dto.Notes
        };
        context.TrackedKeywords.Add(keyword);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTrackedKeywords), null, ToDto(keyword));
    }

    [HttpPost("keywords/check-all")]
    public async Task<ActionResult> CheckAllKeywords()
    {
        await rankChecker.CheckAllKeywordsAsync(context);
        List<TrackedKeyword> keywords = await context.TrackedKeywords
            .OrderByDescending(k => k.UpdatedAtUtc)
            .ToListAsync();
        return Ok(keywords.Select(ToDto).ToList());
    }

    [HttpPut("keywords/{id:guid}")]
    public async Task<ActionResult<TrackedKeywordDto>> UpdateKeyword(Guid id, [FromBody] UpdateTrackedKeywordDto dto)
    {
        TrackedKeyword? keyword = await context.TrackedKeywords.FindAsync(id);
        if (keyword is null)
        {
            return NotFound();
        }

        if (dto.Keyword is not null) { keyword.Keyword = dto.Keyword; }
        if (dto.PageUrl is not null) { keyword.PageUrl = dto.PageUrl; }
        if (dto.Position.HasValue)
        {
            keyword.LastPosition = dto.Position.Value;
            keyword.LastCheckedUtc = DateTime.UtcNow;
        }
        if (dto.Notes is not null) { keyword.Notes = dto.Notes; }

        await context.SaveChangesAsync();
        return Ok(ToDto(keyword));
    }

    [HttpDelete("keywords/{id:guid}")]
    public async Task<IActionResult> DeleteKeyword(Guid id)
    {
        TrackedKeyword? keyword = await context.TrackedKeywords.FindAsync(id);
        if (keyword is null)
        {
            return NotFound();
        }
        context.TrackedKeywords.Remove(keyword);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static TrackedKeywordDto ToDto(TrackedKeyword k) => new(
        k.Id, k.Keyword, k.PageUrl, k.LastPosition, k.LastCheckedUtc, k.Notes
    );
}

public record SeoAnalyseRequest(string Url);

public record SeoAnalysisResult(
    string Url,
    string? Title,
    string? MetaDescription,
    string? MetaKeywords,
    string? CanonicalUrl,
    OpenGraphInfo OpenGraph,
    List<HeadingInfo> Headings,
    int WordCount,
    int ImageCount,
    int ImagesMissingAlt,
    int InternalLinks,
    int ExternalLinks,
    List<string> Issues,
    List<string> Warnings,
    List<string> Passed,
    List<KeywordSuggestion> SuggestedKeywords
);

public record OpenGraphInfo(string? Title, string? Description, string? Image);
public record HeadingInfo(int Level, string Text);
public record KeywordSuggestion(string Keyword, double Score, int Frequency);

public record TrackedKeywordDto(Guid Id, string Keyword, string PageUrl, int? LastPosition, DateTime? LastCheckedUtc, string? Notes);
public record CreateTrackedKeywordDto(string Keyword, string PageUrl, int? Position, string? Notes);
public record UpdateTrackedKeywordDto(string? Keyword, string? PageUrl, int? Position, string? Notes);
