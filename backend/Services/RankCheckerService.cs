using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class RankCheckerService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<RankCheckerService> logger)
{
    private const int MaxPages = 5; // Check up to page 5 (50 results)
    private const string TargetDomain = "edenrelics.co.uk";

    public async Task<int?> CheckRankAsync(string keyword)
    {
        string? apiKey = configuration["Google:SearchApiKey"];
        string? searchEngineId = configuration["Google:SearchEngineId"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(searchEngineId))
        {
            logger.LogWarning("Google Custom Search API not configured");
            return null;
        }

        HttpClient client = httpClientFactory.CreateClient();

        for (int page = 0; page < MaxPages; page++)
        {
            int startIndex = page * 10 + 1;
            string url = $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={searchEngineId}&q={Uri.EscapeDataString(keyword)}&start={startIndex}";

            try
            {
                string json = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("items", out JsonElement items))
                {
                    break;
                }

                int position = startIndex;
                foreach (JsonElement item in items.EnumerateArray())
                {
                    string? link = item.TryGetProperty("link", out JsonElement linkEl) ? linkEl.GetString() : null;
                    if (link is not null && link.Contains(TargetDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation("Keyword '{Keyword}' found at position {Position}", keyword, position);
                        return position;
                    }
                    position++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check rank for '{Keyword}' at page {Page}", keyword, page);
                return null;
            }
        }

        logger.LogInformation("Keyword '{Keyword}' not found in top {Max} results", keyword, MaxPages * 10);
        return null; // Not found in top results
    }

    public async Task CheckAllKeywordsAsync(EdenRelicsDbContext context)
    {
        List<TrackedKeyword> keywords = await context.TrackedKeywords
            .Where(k => !k.IsDeleted)
            .ToListAsync();

        logger.LogInformation("Checking ranks for {Count} keywords", keywords.Count);

        foreach (TrackedKeyword keyword in keywords)
        {
            int? position = await CheckRankAsync(keyword.Keyword);
            keyword.LastPosition = position;
            keyword.LastCheckedUtc = DateTime.UtcNow;

            // Small delay to avoid hitting rate limits
            await Task.Delay(500);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Rank check complete");
    }
}
