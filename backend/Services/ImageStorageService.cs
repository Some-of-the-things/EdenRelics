using Amazon.S3;
using Amazon.S3.Model;

namespace Eden_Relics_BE.Services;

public class ImageStorageService
{
    private readonly AmazonS3Client? _client;
    private readonly string _bucketName;
    private readonly string _publicUrl;
    private readonly ILogger<ImageStorageService> _logger;
    private readonly bool _configured;

    public ImageStorageService(IConfiguration configuration, ILogger<ImageStorageService> logger)
    {
        _logger = logger;
        _bucketName = configuration["R2:BucketName"] ?? "eden-relics-uploads";
        _publicUrl = configuration["R2:PublicUrl"]?.TrimEnd('/') ?? "";

        string? accountId = configuration["R2:AccountId"];
        string? accessKeyId = configuration["R2:AccessKeyId"];
        string? secretAccessKey = configuration["R2:SecretAccessKey"];

        if (!string.IsNullOrWhiteSpace(accountId) &&
            !string.IsNullOrWhiteSpace(accessKeyId) &&
            !string.IsNullOrWhiteSpace(secretAccessKey))
        {
            _client = new AmazonS3Client(
                accessKeyId,
                secretAccessKey,
                new AmazonS3Config
                {
                    ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                    ForcePathStyle = true,
                });
            _configured = true;
            _logger.LogInformation("R2 image storage configured (bucket: {Bucket})", _bucketName);
        }
        else
        {
            _configured = false;
            _logger.LogWarning("R2 image storage not configured — uploads will use local filesystem");
        }
    }

    public bool IsConfigured => _configured;

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
    {
        if (!_configured || _client is null)
        {
            throw new InvalidOperationException("R2 is not configured.");
        }

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            InputStream = stream,
            ContentType = contentType,
        };

        await _client.PutObjectAsync(request);
        _logger.LogInformation("Uploaded {FileName} to R2", fileName);

        return $"{_publicUrl}/{fileName}";
    }

    public async Task DeleteAsync(string fileName)
    {
        if (!_configured || _client is null) { return; }

        try
        {
            await _client.DeleteObjectAsync(_bucketName, fileName);
            _logger.LogInformation("Deleted {FileName} from R2", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {FileName} from R2", fileName);
        }
    }
}
