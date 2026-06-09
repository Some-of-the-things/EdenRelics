using System.Net;
using System.Net.Sockets;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Guards server-side fetches against SSRF and path traversal. Used by the admin
/// SEO analyser and image-analysis endpoints, which fetch caller-supplied URLs/paths.
/// </summary>
public static class UrlSafety
{
    /// <summary>
    /// True only if <paramref name="url"/> is an absolute http/https URL whose host
    /// resolves entirely to public addresses (not loopback, private, link-local,
    /// unique-local, CGNAT, or the cloud-metadata range 169.254.0.0/16).
    /// </summary>
    public static async Task<bool> IsSafePublicUrlAsync(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        IPAddress[] addresses;
        try
        {
            // An IP literal host resolves to itself; a DNS name is resolved here so we
            // validate the address the request will actually connect to.
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
        }
        catch
        {
            return false;
        }

        return addresses.Length > 0 && !addresses.Any(IsBlockedAddress);
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        IPAddress addr = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (IPAddress.IsLoopback(addr))
        {
            return true;
        }

        if (addr.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = addr.GetAddressBytes();
            if (b[0] == 10) { return true; }                          // 10.0.0.0/8
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) { return true; } // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168) { return true; }          // 192.168.0.0/16
            if (b[0] == 169 && b[1] == 254) { return true; }          // 169.254.0.0/16 (link-local + cloud metadata)
            if (b[0] == 0) { return true; }                           // 0.0.0.0/8
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) { return true; } // 100.64.0.0/10 (CGNAT)
        }
        else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal) { return true; }
            byte[] b = addr.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) { return true; }               // fc00::/7 unique-local
        }

        return false;
    }

    /// <summary>
    /// Resolves <paramref name="relativePath"/> under <paramref name="root"/> and returns the
    /// absolute path only if it stays within the root; returns null if it escapes (path traversal).
    /// </summary>
    public static string? ResolveContainedPath(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root);
        string combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        string rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (combined != fullRoot && !combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return combined;
    }
}
