using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class AnalyticsIngestService(IRepository<PageViewDaily> pageViews) : IAnalyticsIngestService
{
    // Hard ceiling on how many *new* (path, isBot, country) buckets we will create in a
    // single day. A real day has at most a few hundred distinct pages; this cap only ever
    // bites under a flood, and then only stops brand-new rows — existing buckets still
    // count. It bounds table growth even for a valid-shaped but high-cardinality attack
    // (e.g. /product/{random-guid}) that the route allow-list below can't reject on shape.
    private const int MaxNewBucketsPerDay = 5000;

    public async Task RecordPageViewAsync(PageViewBeaconDto beacon)
    {
        string path = NormalisePath(beacon.Path);

        // Only record paths that correspond to a real route shape. The beacon fires for
        // any 2xx SSR render, and unknown paths hit the Angular "**" (not-found) route
        // which still renders 200 — so without this an unauthenticated caller could mint
        // one new PageViewDaily row per arbitrary path and grow the table without bound.
        if (!RouteAllowList.IsRecordable(path))
        {
            return;
        }

        string country = NormaliseCountry(beacon.Country);
        bool isBot = BotClassifier.IsBot(beacon.UserAgent, beacon.AsOrganization);
        DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Read-modify-write rather than a SQL upsert: keeps everything inside the
        // repository abstraction and works on the in-memory test provider. Traffic is
        // low enough that the race window is negligible; if two beacons for the same
        // bucket collide on the unique index, we re-read and increment once.
        if (await TryIncrementAsync(date, path, isBot, country))
        {
            return;
        }

        // New bucket: enforce the daily cap before inserting.
        if (await pageViews.Query().CountAsync(p => p.Date == date) >= MaxNewBucketsPerDay)
        {
            return;
        }

        try
        {
            await pageViews.AddAsync(new PageViewDaily
            {
                Date = date,
                Path = path,
                IsBot = isBot,
                Country = country,
                Count = 1,
            });
        }
        catch (DbUpdateException)
        {
            // Lost the insert race — the row now exists, so increment it instead.
            await TryIncrementAsync(date, path, isBot, country);
        }
    }

    private async Task<bool> TryIncrementAsync(DateOnly date, string path, bool isBot, string country)
    {
        PageViewDaily? existing = await pageViews.Query()
            .FirstOrDefaultAsync(p => p.Date == date && p.Path == path && p.IsBot == isBot && p.Country == country);

        if (existing is null)
        {
            return false;
        }

        existing.Count++;
        await pageViews.UpdateAsync(existing);
        return true;
    }

    private static string NormalisePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "/";
        }

        // Drop query string and fragment so the aggregate is per-page, not per-URL.
        string path = raw.Trim();
        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        // Collapse a trailing slash (except the root) so "/about" and "/about/" agree.
        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }
        }

        return path.Length <= 1000 ? path : path[..1000];
    }

    private static string NormaliseCountry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "ZZ";
        }

        string country = raw.Trim().ToUpperInvariant();
        return country.Length <= 8 ? country : country[..8];
    }
}

/// <summary>
/// Whether a normalised request path corresponds to a real front-end route, so that the
/// analytics ingest never records arbitrary attacker-chosen paths (which the SPA serves
/// from its "**" not-found route with a 200 status). Kept in sync with
/// <c>frontend/src/app/app.routes.ts</c>. A dynamic segment ({id}/{slug}/{decade}) is
/// accepted by shape only — its cardinality is bounded separately by the daily cap.
/// </summary>
internal static class RouteAllowList
{
    // Exact single-page routes (no route parameter).
    private static readonly HashSet<string> StaticRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/shop", "/cart", "/login", "/register", "/account", "/blog", "/designers",
        "/care", "/contact", "/about", "/settings", "/forgot-password", "/reset-password",
        "/verify-email", "/admin", "/privacy-policy", "/modern-slavery-policy",
        "/supply-chain-policy", "/returns-policy", "/security", "/terms-conditions",
        "/cookie-policy", "/accessibility-report", "/compliance-report", "/style", "/dresses",
    };

    // Parameterised routes, expressed as the fixed leading segments; the path must have
    // exactly one further (non-empty) dynamic segment after these.
    private static readonly string[][] SingleParamPrefixes =
    [
        ["shop"],                    // /shop/:decade
        ["product"],                 // /product/:id
        ["order-confirmation"],      // /order-confirmation/:id
        ["review"],                  // /review/:orderId
        ["designers"],               // /designers/:slug
        ["style"],                   // /style/:slug
        ["dresses"],                 // /dresses/:slug
        ["collections"],             // /collections/:slug
        ["collections", "preview"],  // /collections/preview/:slug
        ["blog"],                    // /blog/:slug
        ["blog", "preview"],         // /blog/preview/:slug
        ["care", "fabric"],          // /care/fabric/:slug
        ["care", "problem"],         // /care/problem/:slug
    ];

    public static bool IsRecordable(string path)
    {
        if (StaticRoutes.Contains(path))
        {
            return true;
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string[] prefix in SingleParamPrefixes)
        {
            // Fixed prefix segments + exactly one trailing dynamic segment.
            if (segments.Length != prefix.Length + 1)
            {
                continue;
            }

            bool prefixMatches = true;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (!string.Equals(segments[i], prefix[i], StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatches = false;
                    break;
                }
            }

            if (prefixMatches)
            {
                return true;
            }
        }

        return false;
    }
}
