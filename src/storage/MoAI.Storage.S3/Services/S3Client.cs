using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using MoAI.Infra;
using MoAI.Storage.Models;
using System.Net;

namespace MoAI.Storage.Services;

/// <summary>
/// S3 存储对象操作的底层封装.
/// </summary>
public class S3Client : IDisposable
{
    private readonly SystemOptionStorage _storageOption;
    private readonly AmazonS3Client _s3Client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3Client"/> class.
    /// </summary>
    /// <param name="systemOptions">系统配置.</param>
    public S3Client(SystemOptions systemOptions)
    {
        _storageOption = systemOptions.Storage;

        _s3Client = new AmazonS3Client(_storageOption.AccessKeyId, _storageOption.AccessKeySecret, new AmazonS3Config
        {
            ServiceURL = _storageOption.Endpoint,
            ForcePathStyle = _storageOption.ForcePathStyle
        });
    }

    /// <summary>
    /// 生成预签名上传地址.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="contentType">内容类型.</param>
    /// <param name="expiration">有效期.</param>
    /// <returns>预签名地址.</returns>
    public async Task<string> GeneratePreSignedUploadUrlAsync(string objectKey, string? contentType, TimeSpan expiration)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = _storageOption.Bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.PUT
        };

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            request.ContentType = contentType;
        }

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    /// <summary>
    /// 生成预签名下载地址.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="expiration">有效期.</param>
    /// <returns>下载地址.</returns>
    public async Task<Uri> GeneratePreSignedDownloadUrlAsync(string objectKey, TimeSpan expiration)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = _storageOption.Bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.GET
        };

        var url = await _s3Client.GetPreSignedURLAsync(request);
        return new Uri(url);
    }

    /// <summary>
    /// 以流的方式上传文件.
    /// </summary>
    /// <param name="inputStream">文件流.</param>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>.</returns>
    public async Task UploadFileAsync(Stream inputStream, string objectKey, CancellationToken cancellationToken = default)
    {
        using TransferUtility fileTransferUtility = new(_s3Client);
        await fileTransferUtility.UploadAsync(
            new TransferUtilityUploadRequest
            {
                InputStream = inputStream,
                BucketName = _storageOption.Bucket,
                Key = objectKey
            },
            cancellationToken);
    }

    /// <summary>
    /// 检测文件是否存在.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否存在.</returns>
    public async Task<bool> FileExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        GetObjectMetadataRequest request = new()
        {
            BucketName = _storageOption.Bucket,
            Key = objectKey
        };

        try
        {
            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// 读取文件流与元数据.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件流读取结果.</returns>
    public async Task<StorageFileReadResult> ReadFileAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        GetObjectRequest request = new()
        {
            BucketName = _storageOption.Bucket,
            Key = objectKey
        };

        var response = await _s3Client.GetObjectAsync(request, cancellationToken);

        return new StorageFileReadResult
        {
            FileStream = response.ResponseStream,
            ContentType = response.Headers.ContentType ?? "application/octet-stream",
            FileSize = response.ContentLength,
            FileExtension = Path.GetExtension(objectKey) ?? string.Empty,
            ObjectKey = objectKey
        };
    }

    /// <summary>
    /// 读取文件大小.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件大小；不存在时返回 0.</returns>
    public async Task<long> GetFileSizeAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        GetObjectMetadataRequest request = new()
        {
            BucketName = _storageOption.Bucket,
            Key = objectKey
        };

        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return response.ContentLength;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return 0;
        }
    }

    /// <summary>
    /// 批量删除文件.
    /// </summary>
    /// <param name="objectKeys">对象 key 列表.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns></returns>
    public async Task DeleteFilesAsync(IReadOnlyCollection<string> objectKeys, CancellationToken cancellationToken = default)
    {
        if (objectKeys.Count == 0)
        {
            return;
        }

        DeleteObjectsRequest deleteObjectsRequest = new()
        {
            BucketName = _storageOption.Bucket,
            Objects = objectKeys.Select(key => new KeyVersion { Key = key }).ToList()
        };

        await _s3Client.DeleteObjectsAsync(deleteObjectsRequest, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源.
    /// </summary>
    /// <param name="disposing">是否托管资源.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _s3Client.Dispose();
            }

            _disposed = true;
        }
    }
}
