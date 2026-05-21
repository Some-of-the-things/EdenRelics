using Google.Apis.Util.Store;

namespace Eden_Relics_BE.Services;

/// <summary>
/// No-op token store for the Google API OAuth flow. We keep the long-lived
/// refresh token in Fly secrets / configuration, so there's no need to persist
/// transient access tokens — let the SDK refresh on demand each cold start.
/// </summary>
public class NullDataStore : IDataStore
{
    public Task ClearAsync() => Task.CompletedTask;
    public Task DeleteAsync<T>(string key) => Task.CompletedTask;
    public Task<T> GetAsync<T>(string key) => Task.FromResult<T>(default!);
    public Task StoreAsync<T>(string key, T value) => Task.CompletedTask;
}
