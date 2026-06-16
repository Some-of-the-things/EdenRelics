using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace Eden_Relics_BE.Services;

public class CareDraftService(IConfiguration config, ILogger<CareDraftService> logger) : ICareDraftService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Anthropic:ApiKey"]);

    public async Task<CareFabricDraft?> DraftFabricAsync(string name, IReadOnlyList<string> targetKeywords)
    {
        string instruction = $$"""
            You are a vintage-clothing care expert writing for Eden Relics, a UK vintage shop.
            Draft accurate, specific, practical care guidance for the vintage fabric: "{{name}}".
            {{KeywordLine(targetKeywords)}}

            Rules:
            - UK English. Specific to VINTAGE/delicate handling, not generic modern garments.
            - Be accurate and cautious; never recommend anything that could damage a delicate or
              irreplaceable piece. Always assume the reader should spot-test on a hidden seam first.
            - Each prose field 1-3 sentences. "dos"/"donts": 3-5 short imperative items each.
            - metaTitle <= 60 characters; metaDescription <= 155 characters.
            - This is a DRAFT a human expert will review before publishing.

            Return ONLY a JSON object with exactly these keys (no markdown, no commentary):
            {
              "intro": "", "fiberContent": "", "howToIdentify": "", "washing": "",
              "drying": "", "ironing": "", "storing": "", "vintageCautions": "",
              "dos": [], "donts": [], "metaTitle": "", "metaDescription": ""
            }
            """;

        return await CallAsync<CareFabricDraft>(instruction);
    }

    public async Task<CareIssueDraft?> DraftIssueAsync(string name, IReadOnlyList<string> targetKeywords)
    {
        string instruction = $$"""
            You are a vintage-clothing care expert writing for Eden Relics, a UK vintage shop.
            Draft accurate, specific, practical guidance for the vintage-garment problem: "{{name}}".
            {{KeywordLine(targetKeywords)}}

            Rules:
            - UK English. Specific to VINTAGE/delicate garments.
            - Be accurate and cautious; warn clearly about anything that sets stains or damages
              delicate fibres. Always assume the reader should spot-test on a hidden seam first.
            - "causes" and "generalMethod" can be 2-4 sentences; "whatNotToDo" and "whenToSeeAPro"
              1-3 sentences.
            - metaTitle <= 60 characters; metaDescription <= 155 characters.
            - This is a DRAFT a human expert will review before publishing.

            Return ONLY a JSON object with exactly these keys (no markdown, no commentary):
            {
              "causes": "", "generalMethod": "", "whatNotToDo": "", "whenToSeeAPro": "",
              "metaTitle": "", "metaDescription": ""
            }
            """;

        return await CallAsync<CareIssueDraft>(instruction);
    }

    public async Task<FabricIdentifyResult?> IdentifyFabricAsync(
        string base64Image, string mediaType, IReadOnlyList<string> knownFabrics)
    {
        string? apiKey = config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        string knownLine = knownFabrics.Count > 0
            ? $"We have care guides for these fabrics — prefer one of them when it clearly matches: {string.Join("; ", knownFabrics)}."
            : "";

        string instruction = $$"""
            You are a vintage-textiles expert. Look at this clothing photo and identify the most likely
            fabric/material. {{knownLine}} You may also name a fabric that isn't on the list.
            Be honest about uncertainty — identifying fabric from a photo is genuinely hard, so set
            confidence accordingly (0 = guess, 1 = certain).

            Return ONLY a JSON object (no markdown, no commentary):
            {
              "guesses": [ { "name": "", "confidence": 0.0 } ],
              "note": ""
            }
            Give up to 3 guesses, ordered by confidence. "note" is a one-line caveat for the shopper.
            """;

        try
        {
            AnthropicClient client = new(apiKey);
            MessageParameters parameters = new()
            {
                Messages =
                [
                    new Message
                    {
                        Role = RoleType.User,
                        Content =
                        [
                            new ImageContent { Source = new ImageSource { MediaType = mediaType, Data = base64Image } },
                            new TextContent { Text = instruction },
                        ],
                    },
                ],
                MaxTokens = 1024,
                Model = AnthropicModels.Claude45Haiku,
                Stream = false,
                Temperature = 0.2m,
            };

            MessageResponse result = await client.Messages.GetClaudeMessageAsync(parameters);
            string json = ExtractJson(result.Message.ToString().Trim());
            return JsonSerializer.Deserialize<FabricIdentifyResult>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fabric image identification failed");
            return null;
        }
    }

    private async Task<T?> CallAsync<T>(string instruction) where T : class
    {
        string? apiKey = config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Anthropic API key not configured — cannot draft care content");
            return null;
        }

        try
        {
            AnthropicClient client = new(apiKey);
            MessageParameters parameters = new()
            {
                Messages = [new Message { Role = RoleType.User, Content = [new TextContent { Text = instruction }] }],
                MaxTokens = 2048,
                Model = AnthropicModels.Claude45Haiku,
                Stream = false,
                Temperature = 0.4m,
            };

            MessageResponse result = await client.Messages.GetClaudeMessageAsync(parameters);
            string json = ExtractJson(result.Message.ToString().Trim());
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Care draft generation failed");
            return null;
        }
    }

    private static string KeywordLine(IReadOnlyList<string> keywords) =>
        keywords.Count > 0
            ? $"It should help readers searching for: {string.Join("; ", keywords)}."
            : "";

    /// <summary>Pulls the JSON object out of the response even if the model wraps it in prose or fences.</summary>
    private static string ExtractJson(string raw)
    {
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw;
    }
}
