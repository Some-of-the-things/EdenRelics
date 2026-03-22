using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace Eden_Relics_BE.Services;

public class GeoIpService
{
    private readonly HttpClient _http;
    private readonly ILogger<GeoIpService> _logger;
    private static readonly ConcurrentDictionary<string, string?> Cache = new();

    public GeoIpService(HttpClient http, ILogger<GeoIpService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> GetCountryAsync(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        if (IPAddress.TryParse(ipAddress, out IPAddress? parsed))
        {
            if (IPAddress.IsLoopback(parsed) || ipAddress.StartsWith("10.") ||
                ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("172."))
            {
                return null;
            }
        }

        if (Cache.TryGetValue(ipAddress, out string? cached))
        {
            return cached;
        }

        try
        {
            using HttpResponseMessage response = await _http.GetAsync($"http://ip-api.com/json/{ipAddress}?fields=status,country");
            if (response.IsSuccessStatusCode)
            {
                using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("status", out JsonElement status) &&
                    status.GetString() == "success" &&
                    doc.RootElement.TryGetProperty("country", out JsonElement country))
                {
                    string? countryName = country.GetString();
                    Cache.TryAdd(ipAddress, countryName);
                    return countryName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve country for IP {IpAddress}", ipAddress);
        }

        Cache.TryAdd(ipAddress, null);
        return null;
    }
}
