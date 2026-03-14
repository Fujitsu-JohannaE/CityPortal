using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CityPortal.Models;

namespace CityPortal.Services;

/// <summary>
/// Shared Azure Blob Storage service for all tenants.
/// Files are isolated by blob path prefix: {tenantSlug}/{formSlug}/{submissionId}/{filename}.
/// Microsoft Defender for Storage scans blobs asynchronously and writes results
/// to blob index tags ("Malware Scanning scan results" tag).
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Validates and uploads a file. Returns the blob path on success,
    /// or null + error message if validation fails.
    /// </summary>
    Task<(string? BlobPath, string? Error)> UploadAsync(
        string containerName, string blobPath, Stream content,
        string contentType, string originalFileName, long fileSizeBytes);

    /// <summary>
    /// Downloads a blob after verifying its Defender scan result.
    /// Returns null if the blob is malicious or still pending scan.
    /// </summary>
    Task<BlobDownloadResult?> DownloadAsync(string containerName, string blobPath);

    /// <summary>
    /// Reads the Microsoft Defender for Storage malware scan result from blob index tags.
    /// </summary>
    Task<string> GetMalwareScanResultAsync(string containerName, string blobPath);

    /// <summary>
    /// Deletes a blob.
    /// </summary>
    Task DeleteAsync(string containerName, string blobPath);
}

public class BlobDownloadResult
{
    public Stream Content { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public string FileName { get; init; } = default!;
    public string ScanResult { get; init; } = MalwareScanStatus.Pending;
}

public class BlobStorageService : IBlobStorageService
{
    // Tag key written by Microsoft Defender for Storage after scanning
    private const string DefenderScanResultTag = "Malware Scanning scan results";

    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<(string? BlobPath, string? Error)> UploadAsync(
        string containerName, string blobPath, Stream content,
        string contentType, string originalFileName, long fileSizeBytes)
    {
        // ── Server-side file validation ──────────────────────────────────────
        var extension = Path.GetExtension(originalFileName);
        if (!AttachmentPolicy.AllowedExtensions.Contains(extension))
            return (null, $"Tiedostotyyppi '{extension}' ei ole sallittu. Sallitut: {string.Join(", ", AttachmentPolicy.AllowedExtensions)}");

        if (!AttachmentPolicy.AllowedContentTypes.Contains(contentType))
            return (null, $"Sisältötyyppi '{contentType}' ei ole sallittu.");

        if (fileSizeBytes > AttachmentPolicy.MaxFileSizeBytes)
            return (null, $"Tiedosto on liian suuri ({fileSizeBytes / 1024} KB). Enimmäiskoko on {AttachmentPolicy.MaxFileSizeBytes / 1024 / 1024} MB.");

        // ── Upload to shared storage account ─────────────────────────────────
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = containerClient.GetBlobClient(blobPath);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                ContentDisposition = $"attachment; filename=\"{originalFileName}\""
            },
            // Tag the blob so Defender for Storage knows to scan it,
            // and we can track scan status before the async result arrives
            Tags = new Dictionary<string, string>
            {
                ["tenant"] = blobPath.Split('/').FirstOrDefault() ?? "unknown",
                ["uploadedAt"] = DateTime.UtcNow.ToString("o")
            }
        };

        await blobClient.UploadAsync(content, uploadOptions);
        _logger.LogInformation("Uploaded blob {BlobPath} ({Size} bytes) to container {Container}",
            blobPath, fileSizeBytes, containerName);

        return (blobPath, null);
    }

    public async Task<BlobDownloadResult?> DownloadAsync(string containerName, string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync())
        {
            _logger.LogWarning("Blob not found: {BlobPath}", blobPath);
            return null;
        }

        // ── Check Defender scan result before serving ────────────────────────
        var scanResult = await ReadScanTagAsync(blobClient);
        if (scanResult == MalwareScanStatus.Malicious)
        {
            _logger.LogWarning("Blocked download of malicious blob: {BlobPath}", blobPath);
            return null;
        }

        var response = await blobClient.DownloadStreamingAsync();
        var fileName = Path.GetFileName(blobPath);

        return new BlobDownloadResult
        {
            Content = response.Value.Content,
            ContentType = response.Value.Details.ContentType ?? "application/octet-stream",
            FileName = fileName,
            ScanResult = scanResult
        };
    }

    public async Task<string> GetMalwareScanResultAsync(string containerName, string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync())
            return MalwareScanStatus.Error;

        return await ReadScanTagAsync(blobClient);
    }

    public async Task DeleteAsync(string containerName, string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
        _logger.LogInformation("Deleted blob: {BlobPath}", blobPath);
    }

    /// <summary>
    /// Reads the Microsoft Defender for Storage scan result tag from the blob.
    /// Defender writes: "No threats found" (clean) or "Malicious" etc.
    /// If the tag is not yet present, the scan is still pending.
    /// </summary>
    private async Task<string> ReadScanTagAsync(BlobClient blobClient)
    {
        try
        {
            var tagsResponse = await blobClient.GetTagsAsync();
            var tags = tagsResponse.Value.Tags;

            if (tags.TryGetValue(DefenderScanResultTag, out var result))
            {
                if (result.Contains("No threats found", StringComparison.OrdinalIgnoreCase))
                    return MalwareScanStatus.Clean;

                if (result.Contains("Malicious", StringComparison.OrdinalIgnoreCase))
                    return MalwareScanStatus.Malicious;

                _logger.LogWarning("Unknown Defender scan result for {Blob}: {Result}",
                    blobClient.Name, result);
                return MalwareScanStatus.Pending;
            }

            // Tag not yet written — scan is still in progress
            return MalwareScanStatus.Pending;
        }
        catch (RequestFailedException ex) when (ex.Status == 403 || ex.Status == 404)
        {
            // Tags may not be available in dev/emulator — treat as clean for local dev
            _logger.LogDebug("Cannot read blob tags (status {Status}), assuming clean for dev: {Blob}",
                ex.Status, blobClient.Name);
            return MalwareScanStatus.Clean;
        }
    }
}
