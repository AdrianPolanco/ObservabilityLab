using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using System.Net;

namespace ObservabilityLab.Shared.Services;

public class MinIOService(IMinioClient minioClient, ILogger<MinIOService> logger)
{
    /// <summary>
    /// Uploads <paramref name="bytes"/> directly from memory to MinIO — no temp file on disk.
    /// <paramref name="objectName"/> is the S3 object key stored in the bucket.
    /// </summary>
    public async Task<(bool isSuccess, string? fileUrl)> UploadAsync(
        string objectName,
        byte[] bytes,
        string bucketName,
        string contentType,
        CancellationToken cancellationToken)
    {
        // Wrap the existing buffer — zero copy, zero disk I/O.
        using var stream = new MemoryStream(bytes, writable: false);

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(bytes.Length)
            .WithContentType(contentType);

        var response = await minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

        var isSuccess = response.ResponseStatusCode is HttpStatusCode.OK or HttpStatusCode.Created;

        logger.LogInformation(
            "Uploaded {ObjectName} to bucket {Bucket} ({Size} bytes), status {Status}.",
            objectName, bucketName, response.Size, response.ResponseStatusCode);

        string? fileUrl = null;

        if (isSuccess)
        {
            fileUrl = await this.GetFileUrlAsync(objectName, bucketName, 7200, cancellationToken);
        }


        return (isSuccess, fileUrl);
    }

    /// <summary>
    /// Returns a time-limited presigned GET URL for the object. Anyone with the URL can download
    /// the file until it expires — no MinIO credentials required. MinIO caps expiry at 7 days.
    /// </summary>
    private async Task<string> GetFileUrlAsync(
        string objectName,
        string bucketName,
        int expirySeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        // PresignedGetObjectAsync does not accept a CancellationToken in the SDK;
        // the parameter is kept on our signature for call-site symmetry and forward-compat.
        var url = await minioClient.PresignedGetObjectAsync(args);

        logger.LogInformation(
            "Generated presigned URL for {ObjectName} in {Bucket} (expires in {Expiry}s).",
            objectName, bucketName, expirySeconds);

        return url;
    }

    /// <summary>
    /// Downloads the object from MinIO into memory and returns its raw bytes
    /// (e.g. to attach a PDF to an email).
    /// </summary>
    public async Task<byte[]> GetFileAsync(
        string objectName,
        string bucketName,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, ct) => await stream.CopyToAsync(ms, ct));

        await minioClient.GetObjectAsync(args, cancellationToken);

        logger.LogInformation(
            "Downloaded {ObjectName} from {Bucket} ({Size} bytes).",
            objectName, bucketName, ms.Length);

        return ms.ToArray();
    }

    /// <summary>
    /// Creates the bucket if it does not already exist. Safe to call on every startup
    /// (idempotent). Call once during bootstrap — not on every message.
    /// </summary>
    public async Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken)
    {
        var exists = await minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName), cancellationToken);

        if (!exists)
        {
            await minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName), cancellationToken);

            logger.LogInformation("Created MinIO bucket {Bucket}.", bucketName);
        }
        else
        {
            logger.LogInformation("MinIO bucket {Bucket} already exists.", bucketName);
        }
    }
}
