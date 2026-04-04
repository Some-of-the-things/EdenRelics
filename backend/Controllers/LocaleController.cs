using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocaleController(GeoIpService geoIp) : ControllerBase
{
    private static readonly Dictionary<string, CountryLocaleInfo> CountryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Europe
        ["United Kingdom"] = new("GB", "GBP", "en-GB", "£"),
        ["Ireland"] = new("IE", "EUR", "en-IE", "\u20ac"),
        ["France"] = new("FR", "EUR", "fr-FR", "\u20ac"),
        ["Germany"] = new("DE", "EUR", "de-DE", "\u20ac"),
        ["Netherlands"] = new("NL", "EUR", "nl-NL", "\u20ac"),
        ["Belgium"] = new("BE", "EUR", "nl-BE", "\u20ac"),
        ["Spain"] = new("ES", "EUR", "es-ES", "\u20ac"),
        ["Portugal"] = new("PT", "EUR", "pt-PT", "\u20ac"),
        ["Italy"] = new("IT", "EUR", "it-IT", "\u20ac"),
        ["Austria"] = new("AT", "EUR", "de-AT", "\u20ac"),
        ["Switzerland"] = new("CH", "CHF", "de-CH", "CHF"),
        ["Sweden"] = new("SE", "SEK", "sv-SE", "kr"),
        ["Denmark"] = new("DK", "DKK", "da-DK", "kr"),
        ["Norway"] = new("NO", "NOK", "nb-NO", "kr"),
        ["Finland"] = new("FI", "EUR", "fi-FI", "\u20ac"),
        ["Poland"] = new("PL", "PLN", "pl-PL", "z\u0142"),
        ["Czech Republic"] = new("CZ", "CZK", "cs-CZ", "K\u010d"),
        ["Czechia"] = new("CZ", "CZK", "cs-CZ", "K\u010d"),
        ["Greece"] = new("GR", "EUR", "el-GR", "\u20ac"),
        ["Croatia"] = new("HR", "EUR", "hr-HR", "\u20ac"),
        ["Hungary"] = new("HU", "HUF", "hu-HU", "Ft"),
        ["Romania"] = new("RO", "RON", "ro-RO", "lei"),
        ["Luxembourg"] = new("LU", "EUR", "fr-LU", "\u20ac"),
        // North America
        ["United States"] = new("US", "USD", "en-US", "$"),
        ["Canada"] = new("CA", "CAD", "en-CA", "CA$"),
        // Australasia
        ["Australia"] = new("AU", "AUD", "en-AU", "A$"),
        ["New Zealand"] = new("NZ", "NZD", "en-NZ", "NZ$"),
        // Asia
        ["Japan"] = new("JP", "JPY", "ja-JP", "\u00a5"),
        ["South Korea"] = new("KR", "KRW", "ko-KR", "\u20a9"),
        ["Singapore"] = new("SG", "SGD", "en-SG", "S$"),
        ["Hong Kong"] = new("HK", "HKD", "en-HK", "HK$"),
        ["Taiwan"] = new("TW", "TWD", "zh-TW", "NT$"),
        ["Malaysia"] = new("MY", "MYR", "ms-MY", "RM"),
        ["Thailand"] = new("TH", "THB", "th-TH", "\u0e3f"),
        // Middle East
        ["United Arab Emirates"] = new("AE", "AED", "ar-AE", "AED"),
        ["Saudi Arabia"] = new("SA", "SAR", "ar-SA", "SAR"),
        ["Qatar"] = new("QA", "QAR", "ar-QA", "QAR"),
        ["Israel"] = new("IL", "ILS", "he-IL", "\u20aa"),
        // Rest of World
        ["South Africa"] = new("ZA", "ZAR", "en-ZA", "R"),
        ["Mexico"] = new("MX", "MXN", "es-MX", "MX$"),
    };

    // Approximate rates vs GBP (updated periodically — not for billing, just display)
    private static readonly Dictionary<string, decimal> ExchangeRates = new()
    {
        ["GBP"] = 1m,
        ["EUR"] = 1.17m,
        ["USD"] = 1.27m,
        ["CAD"] = 1.72m,
        ["AUD"] = 1.94m,
        ["NZD"] = 2.12m,
        ["JPY"] = 191m,
        ["KRW"] = 1720m,
        ["SGD"] = 1.70m,
        ["HKD"] = 9.92m,
        ["TWD"] = 41m,
        ["MYR"] = 5.65m,
        ["THB"] = 44m,
        ["CHF"] = 1.12m,
        ["SEK"] = 13.2m,
        ["DKK"] = 8.72m,
        ["NOK"] = 13.6m,
        ["PLN"] = 5.10m,
        ["CZK"] = 29.5m,
        ["HUF"] = 470m,
        ["RON"] = 5.82m,
        ["AED"] = 4.67m,
        ["SAR"] = 4.76m,
        ["QAR"] = 4.63m,
        ["ILS"] = 4.60m,
        ["ZAR"] = 23.5m,
        ["MXN"] = 21.7m,
    };

    [HttpGet("detect")]
    public async Task<ActionResult> Detect()
    {
        string? ip = Request.Headers["Fly-Client-IP"].FirstOrDefault()
            ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        string? countryName = await geoIp.GetCountryAsync(ip);

        if (countryName is not null && CountryMap.TryGetValue(countryName, out CountryLocaleInfo? info))
        {
            decimal rate = ExchangeRates.GetValueOrDefault(info.Currency, 1m);
            return Ok(new
            {
                detected = true,
                countryCode = info.CountryCode,
                countryName,
                currency = info.Currency,
                currencySymbol = info.CurrencySymbol,
                locale = info.Locale,
                exchangeRate = rate
            });
        }

        // Default to UK
        return Ok(new
        {
            detected = countryName is not null,
            countryCode = "GB",
            countryName = countryName ?? "United Kingdom",
            currency = "GBP",
            currencySymbol = "\u00a3",
            locale = "en-GB",
            exchangeRate = 1m
        });
    }

    [HttpGet("rates")]
    public ActionResult GetRates()
    {
        return Ok(ExchangeRates);
    }
}

public record CountryLocaleInfo(string CountryCode, string Currency, string Locale, string CurrencySymbol);
