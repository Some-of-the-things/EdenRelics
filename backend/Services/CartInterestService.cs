using System.Collections.Concurrent;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Tracks how many active sessions have a product in their cart.
/// Entries expire after 30 minutes of inactivity.
/// </summary>
public class CartInterestService
{
    private readonly ConcurrentDictionary<string, DateTime> _interests = new();
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(30);

    /// <summary>Record that a session has this product in their cart.</summary>
    public void Add(Guid productId, string sessionId)
    {
        string key = $"{productId}:{sessionId}";
        _interests[key] = DateTime.UtcNow;
    }

    /// <summary>Record that a session removed this product from their cart.</summary>
    public void Remove(Guid productId, string sessionId)
    {
        string key = $"{productId}:{sessionId}";
        _interests.TryRemove(key, out _);
    }

    /// <summary>Get the number of active sessions with this product in their cart.</summary>
    public int GetCount(Guid productId)
    {
        string prefix = $"{productId}:";
        DateTime cutoff = DateTime.UtcNow - Expiry;
        int count = 0;

        foreach (KeyValuePair<string, DateTime> kvp in _interests)
        {
            if (kvp.Key.StartsWith(prefix))
            {
                if (kvp.Value >= cutoff)
                {
                    count++;
                }
                else
                {
                    _interests.TryRemove(kvp.Key, out _);
                }
            }
        }

        return count;
    }
}
