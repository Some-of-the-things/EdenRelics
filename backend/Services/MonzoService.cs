using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class MonzoService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MonzoService> logger)
{
    private const string BaseUrl = "https://api.monzo.com";
    private const string AuthUrl = "https://auth.monzo.com";
    private const string TokenUrl = "https://api.monzo.com/oauth2/token";

    private string ClientId => configuration["Monzo:ClientId"] ?? "";
    private string ClientSecret => configuration["Monzo:ClientSecret"] ?? "";
    private string RedirectUri => configuration["Monzo:RedirectUri"] ?? "";

    public bool IsOAuthConfigured => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);

    public string GetAuthorizeUrl(string state)
    {
        return $"{AuthUrl}/?client_id={Uri.EscapeDataString(ClientId)}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&state={Uri.EscapeDataString(state)}";
    }

    public async Task<(MonzoTokenResponse? Token, string? Error)> ExchangeCodeAsync(string code)
    {
        FormUrlEncodedContent content = new([
            new("grant_type", "authorization_code"),
            new("client_id", ClientId),
            new("client_secret", ClientSecret),
            new("redirect_uri", RedirectUri),
            new("code", code),
        ]);

        HttpResponseMessage response = await httpClient.PostAsync(TokenUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Monzo token exchange failed: {Status} {Body}", response.StatusCode, body);
            return (null, body);
        }

        string json = await response.Content.ReadAsStringAsync();
        return (JsonSerializer.Deserialize<MonzoTokenResponse>(json), null);
    }

    public async Task<MonzoTokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        FormUrlEncodedContent content = new([
            new("grant_type", "refresh_token"),
            new("client_id", ClientId),
            new("client_secret", ClientSecret),
            new("refresh_token", refreshToken),
        ]);

        HttpResponseMessage response = await httpClient.PostAsync(TokenUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Monzo token refresh failed: {Status} {Body}", response.StatusCode, body);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MonzoTokenResponse>(json);
    }

    public async Task<List<MonzoAccountResponse>> GetAccountsAsync(string accessToken)
    {
        // Try business accounts first, fall back to all accounts
        List<MonzoAccountResponse> accounts = await FetchAccountsAsync(accessToken, "uk_business");
        if (accounts.Count == 0)
        {
            accounts = await FetchAccountsAsync(accessToken, null);
        }
        return accounts;
    }

    private async Task<List<MonzoAccountResponse>> FetchAccountsAsync(string accessToken, string? accountType)
    {
        string url = $"{BaseUrl}/accounts";
        if (accountType is not null)
        {
            url += $"?account_type={accountType}";
        }

        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Monzo accounts request failed: {Status}", response.StatusCode);
            return [];
        }

        string json = await response.Content.ReadAsStringAsync();
        MonzoAccountListResponse? result = JsonSerializer.Deserialize<MonzoAccountListResponse>(json);
        return result?.Accounts ?? [];
    }

    public async Task<string?> EnsureValidTokenAsync(EdenRelicsDbContext context)
    {
        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();
        if (token is null) { return null; }

        if (token.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return token.AccessToken;
        }

        MonzoTokenResponse? refreshed = await RefreshTokenAsync(token.RefreshToken);
        if (refreshed is null)
        {
            logger.LogWarning("Failed to refresh Monzo token — connection may need re-authorizing");
            return null;
        }

        token.AccessToken = refreshed.AccessToken;
        token.RefreshToken = refreshed.RefreshToken;
        token.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
        await context.SaveChangesAsync();

        return token.AccessToken;
    }

    public async Task<MonzoBalanceResponse?> GetBalanceAsync(string accessToken, string accountId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl}/balance?account_id={accountId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Monzo balance request failed: {Status}", response.StatusCode);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MonzoBalanceResponse>(json);
    }

    public async Task<List<MonzoPotResponse>> GetPotsAsync(string accessToken, string accountId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl}/pots?current_account_id={accountId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Monzo pots request failed: {Status}", response.StatusCode);
            return [];
        }

        string json = await response.Content.ReadAsStringAsync();
        MonzoPotListResponse? result = JsonSerializer.Deserialize<MonzoPotListResponse>(json);
        return result?.Pots?.Where(p => !p.Deleted).ToList() ?? [];
    }

    public async Task<List<MonzoTransactionResponse>> GetTransactionsAsync(
        string accessToken, string accountId, DateTime? since = null, DateTime? before = null)
    {
        string url = $"{BaseUrl}/transactions?account_id={accountId}&expand[]=merchant&limit=100";
        if (since.HasValue)
        {
            url += $"&since={since.Value:yyyy-MM-ddTHH:mm:ssZ}";
        }
        if (before.HasValue)
        {
            url += $"&before={before.Value:yyyy-MM-ddTHH:mm:ssZ}";
        }

        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Monzo transactions request failed: {Status}", response.StatusCode);
            return [];
        }

        string json = await response.Content.ReadAsStringAsync();
        MonzoTransactionListResponse? result = JsonSerializer.Deserialize<MonzoTransactionListResponse>(json);
        return result?.Transactions ?? [];
    }

    public async Task<bool> AnnotateTransactionAsync(string accessToken, string transactionId, string? notes, string? tags)
    {
        List<KeyValuePair<string, string>> formData = [];
        if (notes is not null)
        {
            formData.Add(new("metadata[notes]", notes));
        }
        if (tags is not null)
        {
            formData.Add(new("metadata[tags]", tags));
        }

        HttpRequestMessage request = new(HttpMethod.Patch, $"{BaseUrl}/transactions/{transactionId}")
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Monzo annotate request failed: {Status}", response.StatusCode);
        }
        return response.IsSuccessStatusCode;
    }
}

// OAuth token response
public class MonzoTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
}

// Account models
public class MonzoAccountListResponse
{
    [JsonPropertyName("accounts")]
    public List<MonzoAccountResponse> Accounts { get; set; } = [];
}

public class MonzoAccountResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

// Balance
public class MonzoBalanceResponse
{
    [JsonPropertyName("balance")]
    public long Balance { get; set; }

    [JsonPropertyName("total_balance")]
    public long TotalBalance { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "GBP";

    [JsonPropertyName("spend_today")]
    public long SpendToday { get; set; }
}

// Transaction models
public class MonzoTransactionListResponse
{
    [JsonPropertyName("transactions")]
    public List<MonzoTransactionResponse> Transactions { get; set; } = [];
}

public class MonzoTransactionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "GBP";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("merchant")]
    public MonzoMerchantResponse? Merchant { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("is_load")]
    public bool IsLoad { get; set; }

    [JsonPropertyName("decline_reason")]
    public string? DeclineReason { get; set; }

    [JsonPropertyName("settled")]
    public string? Settled { get; set; }
}

public class MonzoMerchantResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

// Pot models
public class MonzoPotListResponse
{
    [JsonPropertyName("pots")]
    public List<MonzoPotResponse> Pots { get; set; } = [];
}

public class MonzoPotResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("balance")]
    public long Balance { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "GBP";

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}
