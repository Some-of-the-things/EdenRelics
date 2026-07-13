using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace EdenRelics.SellerTool.Api;

/// <summary>Stores captured label/flat-lay images (the archive/moat) and returns their object key,
/// which is saved on the EvidenceRecord. Behind an interface so it's faked in tests and the real
/// Cloudflare R2 wiring lives in one place.</summary>
public interface IImageStore
{
    Task<string> PutAsync(Stream content, string contentType, string keyPrefix, CancellationToken ct = default);
}

public sealed class R2Options
{
    public const string SectionName = "R2";
    public string? Endpoint { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}

/// <summary>Cloudflare R2 (S3-compatible) image store. Config-driven; not exercised in tests (faked).</summary>
public sealed class R2ImageStore(IOptions<R2Options> options) : IImageStore
{
    private readonly R2Options _options = options.Value;

    // Built lazily so a missing config doesn't throw at construction (tests replace this store).
    private IAmazonS3? _client;
    private IAmazonS3 Client => _client ??= new AmazonS3Client(
        new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
        new AmazonS3Config { ServiceURL = _options.Endpoint, ForcePathStyle = true });

    public async Task<string> PutAsync(Stream content, string contentType, string keyPrefix, CancellationToken ct = default)
    {
        string key = $"{keyPrefix.Trim('/')}/{Guid.NewGuid():N}{ExtensionFor(contentType)}";
        await Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // R2 doesn't support streaming (chunked) signatures.
            DisablePayloadSigning = true,
        }, ct);
        return key;
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/heic" => ".heic",
        _ => "",
    };
}
