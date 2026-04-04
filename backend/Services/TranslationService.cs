using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace Eden_Relics_BE.Services;

public class TranslationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TranslationService> _logger;

    // Target locales for translation (language code -> display name)
    public static readonly Dictionary<string, string> SupportedLocales = new()
    {
        ["en"] = "English",
        ["fr"] = "French",
        ["de"] = "German",
        ["es"] = "Spanish",
        ["it"] = "Italian",
        ["nl"] = "Dutch",
        ["pt"] = "Portuguese",
        ["sv"] = "Swedish",
        ["da"] = "Danish",
        ["nb"] = "Norwegian",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
    };

    public TranslationService(IConfiguration config, ILogger<TranslationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Translate a batch of key-value content pairs into all supported locales.
    /// Returns a dictionary of "locale.originalKey" -> translated value.
    /// Skips English (source language) and non-translatable content (dates, URLs, numbers).
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> content)
    {
        string? apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Anthropic API key not configured — skipping translations");
            return [];
        }

        // Filter to only text content worth translating (skip URLs, numbers, dates, HTML-heavy policy content)
        Dictionary<string, string> translatable = content
            .Where(kv => IsTranslatable(kv.Key, kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (translatable.Count == 0)
        {
            return [];
        }

        Dictionary<string, string> allTranslations = [];

        foreach ((string locale, string language) in SupportedLocales)
        {
            if (locale == "en")
            {
                continue; // Source language
            }

            try
            {
                Dictionary<string, string> translated = await TranslateToLocaleAsync(
                    apiKey, translatable, locale, language);

                foreach ((string key, string value) in translated)
                {
                    allTranslations[$"{locale}.{key}"] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate content to {Locale}", locale);
            }
        }

        return allTranslations;
    }

    /// <summary>
    /// Translate a single text string into all supported locales.
    /// Returns a dictionary of locale -> translated text.
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateTextAsync(string text)
    {
        string? apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        Dictionary<string, string> translations = [];

        foreach ((string locale, string language) in SupportedLocales)
        {
            if (locale == "en")
            {
                continue;
            }

            try
            {
                string translated = await TranslateSingleAsync(apiKey, text, language);
                translations[locale] = translated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate text to {Language}", language);
            }
        }

        return translations;
    }

    private async Task<Dictionary<string, string>> TranslateToLocaleAsync(
        string apiKey, Dictionary<string, string> content, string locale, string language)
    {
        var client = new AnthropicClient(apiKey);

        // Build a compact representation for the prompt
        string entries = string.Join("\n", content.Select(kv => $"{kv.Key}|||{kv.Value}"));

        var messages = new List<Message>
        {
            new()
            {
                Role = RoleType.User,
                Content =
                [
                    new TextContent
                    {
                        Text = $"""
                            Translate the following content to {language}. Each line has a key and value separated by |||.
                            Return the same format: key|||translated_value, one per line.
                            Preserve any HTML tags exactly as they are — only translate the text content within tags.
                            Keep brand names, product names, URLs, email addresses, phone numbers, and company names unchanged.
                            Keep the same tone — this is for a vintage clothing e-commerce shop.
                            Do NOT translate keys, only values.

                            {entries}
                            """
                    }
                ]
            }
        };

        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = 4096,
            Model = AnthropicModels.Claude45Haiku,
            Stream = false,
            Temperature = 0.2m,
        };

        var result = await client.Messages.GetClaudeMessageAsync(parameters);
        string response = result.Message.ToString().Trim();

        Dictionary<string, string> translated = [];
        foreach (string line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int sep = line.IndexOf("|||");
            if (sep > 0)
            {
                string key = line[..sep].Trim();
                string value = line[(sep + 3)..].Trim();
                if (content.ContainsKey(key) && !string.IsNullOrWhiteSpace(value))
                {
                    translated[key] = value;
                }
            }
        }

        _logger.LogInformation("Translated {Count}/{Total} entries to {Language}",
            translated.Count, content.Count, language);

        return translated;
    }

    private async Task<string> TranslateSingleAsync(string apiKey, string text, string language)
    {
        var client = new AnthropicClient(apiKey);

        var messages = new List<Message>
        {
            new()
            {
                Role = RoleType.User,
                Content =
                [
                    new TextContent
                    {
                        Text = $"""
                            Translate the following text to {language}. This is for a vintage clothing e-commerce shop.
                            Preserve any HTML tags exactly — only translate the text within tags.
                            Keep brand names, product names, URLs, email addresses, and company names unchanged.
                            Return ONLY the translated text, nothing else.

                            {text}
                            """
                    }
                ]
            }
        };

        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = 2048,
            Model = AnthropicModels.Claude45Haiku,
            Stream = false,
            Temperature = 0.2m,
        };

        var result = await client.Messages.GetClaudeMessageAsync(parameters);
        return result.Message.ToString().Trim();
    }

    private static bool IsTranslatable(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Skip locale-prefixed keys (already translated)
        if (key.Length > 3 && key[2] == '.')
        {
            string prefix = key[..2];
            if (SupportedLocales.ContainsKey(prefix))
            {
                return false;
            }
        }

        // Skip keys that contain non-translatable values
        if (key.Contains(".updated") || key.Contains(".email") || key.Contains(".phone"))
        {
            return false;
        }

        // Skip purely numeric or URL values
        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return false;
        }

        // Skip very long policy/report HTML content (translate separately on demand)
        if (key.Contains(".content") && value.Length > 500)
        {
            return false;
        }

        // Skip company registration numbers
        if (key.Contains("company.line"))
        {
            return false;
        }

        return true;
    }
}
