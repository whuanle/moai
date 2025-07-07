//// <copyright file="S3PrivateStorage.cs" company="MoAI">
//// Copyright (c) MoAI. All rights reserved.
//// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//// Github link: https://github.com/whuanle/moai
//// </copyright>

//using Amazon.S3;
//using Amazon.S3.Model;
//using Amazon.S3.Transfer;
//using MoAI.Infra;
//using MoAI.Store.Services;
//using System.Diagnostics;
//using System.Net;

//namespace MoAI.Storage.Services;

///// <summary>
///// S3 存储对接.
///// </summary>
//public class S3PrivateStorage : IPrivateFileStorage, IDisposable
//{
//    private readonly SystemOptionsStorageS3 _storageOption;
//    private readonly AmazonS3Client _s3Client;
//    private bool disposedValue;

//    /// <summary>
//    /// Initializes a new instance of the <see cref="S3PrivateStorage"/> class.
//    /// </summary>
//    /// <param name="systemOptions"></param>
//    public S3PrivateStorage(SystemOptions systemOptions)
//    {
//        _storageOption = new SystemOptionsStorageS3();

//        _s3Client = new AmazonS3Client(_storageOption.AccessKeyId, _storageOption.AccessKeySecret, new AmazonS3Config
//        {
//            ServiceURL = _storageOption.Endpoint,
//            ForcePathStyle = _storageOption.ForcePathStyle,
//            UseHttp = true
//        });
//    }

//    /// <inheritdoc/>
//    public async Task UploadFileAsync(Stream inputStream, string objectKey)
//    {
//        using TransferUtility? fileTransferUtility = new(_s3Client);
//        await fileTransferUtility.UploadAsync(inputStream, _storageOption.Bucket, objectKey);
//    }

//    /// <inheritdoc/>
//    public async Task<string> GeneratePreSignedUploadUrlAsync(FileObject fileObject)
//    {
//        GetPreSignedUrlRequest request = new()
//        {
//            BucketName = _storageOption.Bucket,
//            Key = fileObject.ObjectKey,
//            Expires = DateTime.UtcNow.Add(fileObject.ExpiryDuration),
//            Verb = HttpVerb.PUT,
//        };

//        // 限制上传的文件类型.
//        if (!string.IsNullOrWhiteSpace(fileObject.ContentType))
//        {
//            request.ContentType = fileObject.ContentType;
//        }

//        // 可以在对象 metadata 中添加最大的文件大小信息
//        // request.Headers["x-amz-meta-max-file-size"] = fileObject.MaxFileSize.ToString();
//        string url = await _s3Client.GetPreSignedURLAsync(request);
//        return url;
//    }

//    /// <inheritdoc/>
//    public async Task<Uri> GetFileUrlAsync(string objectKey, TimeSpan expiryDuration)
//    {
//        GetPreSignedUrlRequest? request = new()
//        {
//            BucketName = _storageOption.Bucket,
//            Key = objectKey,
//            Verb = HttpVerb.GET, // 指定只能用于下载
//            Expires = DateTime.Now.Add(expiryDuration)
//        };

//        var url = await _s3Client.GetPreSignedURLAsync(request);
//        return new Uri(url);
//    }

//    /// <inheritdoc/>
//    public async Task<IReadOnlyDictionary<string, Uri>> GetFileUrlAsync(IEnumerable<string> objectKeys, TimeSpan expiryDuration)
//    {
//        IEnumerable<Task<KeyValuePair<string, Uri>>>? tasks = objectKeys.Select(async key =>
//        {
//            GetPreSignedUrlRequest? request = new()
//            {
//                BucketName = _storageOption.Bucket,
//                Key = key,
//                Verb = HttpVerb.GET, // 指定只能用于下载
//                Expires = DateTime.Now.Add(expiryDuration)
//            };
//            string? url = await _s3Client.GetPreSignedURLAsync(request);
//            return new KeyValuePair<string, Uri>(key, new Uri(url));
//        });

//        KeyValuePair<string, Uri>[]? urls = await Task.WhenAll(tasks);

//        return urls.ToDictionary();
//    }

//    /// <inheritdoc/>
//    public async Task<bool> FileExistsAsync(string objectKey)
//    {
//        GetObjectMetadataRequest? request = new()
//        {
//            BucketName = _storageOption.Bucket,
//            Key = objectKey
//        };
//        try
//        {
//            await _s3Client.GetObjectMetadataAsync(request);
//            return true;
//        }
//        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
//        {
//            return false;
//        }
//    }

//    /// <inheritdoc/>
//    public async Task DownloadAsync(string objectKey, string filePath)
//    {
//        GetObjectRequest? request = new()
//        {
//            BucketName = _storageOption.Bucket,
//            Key = objectKey
//        };

//        using var response = await _s3Client.GetObjectAsync(request);
//        await response.WriteResponseStreamToFileAsync(filePath, true, CancellationToken.None);
//    }

//    /// <inheritdoc/>
//    public async Task<IReadOnlyDictionary<string, bool>> FilesExistsAsync(IEnumerable<string> objectKeys)
//    {
//        IEnumerable<Task<KeyValuePair<string, bool>>>? tasks = objectKeys.Select(async key =>
//        {
//            GetObjectMetadataRequest? request = new()
//            {
//                BucketName = _storageOption.Bucket,
//                Key = key
//            };
//            try
//            {
//                await _s3Client.GetObjectMetadataAsync(request);
//                return new KeyValuePair<string, bool>(key, true);
//            }
//            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
//            {
//                return new KeyValuePair<string, bool>(key, false);
//            }
//        });

//        var results = await Task.WhenAll(tasks);
//        return results.ToDictionary();
//    }

//    /// <inheritdoc/>
//    public async Task<long> GetFileSizeAsync(string objectKey)
//    {
//        GetObjectMetadataRequest? request = new()
//        {
//            BucketName = _storageOption.Bucket,
//            Key = objectKey
//        };

//        try
//        {
//            var response = await _s3Client.GetObjectMetadataAsync(request);

//            return response.ContentLength;
//        }
//        catch (AmazonS3Exception ex)
//        {
//            Debug.WriteLine(ex);
//            return 0;
//        }
//        catch (Exception ex)
//        {
//            Debug.WriteLine(ex);

//            return 0;
//        }
//    }

//    /// <inheritdoc/>
//    public async Task<IReadOnlyDictionary<string, long>> GetFileSizeAsync(IEnumerable<string> objectKeys)
//    {
//        IEnumerable<Task<KeyValuePair<string, long>>>? tasks = objectKeys.Select(async key =>
//        {
//            GetObjectMetadataRequest? request = new()
//            {
//                BucketName = _storageOption.Bucket,
//                Key = key
//            };
//            GetObjectMetadataResponse? response = await _s3Client.GetObjectMetadataAsync(request);
//            return new KeyValuePair<string, long>(key, response.ContentLength);
//        });
//        KeyValuePair<string, long>[]? sizes = await Task.WhenAll(tasks);
//        return sizes.ToDictionary();
//    }

//    /// <inheritdoc/>
//    public async Task DeleteFilesAsync(IEnumerable<string> objectKeys)
//    {
//        DeleteObjectsRequest? deleteObjectsRequest = new()
//        {
//            BucketName = _storageOption.Bucket,
//            Objects = objectKeys.Select(key => new KeyVersion { Key = key }).ToList()
//        };
//        await _s3Client.DeleteObjectsAsync(deleteObjectsRequest);
//    }

//    /// <inheritdoc/>
//    public void Dispose()
//    {
//        Dispose(disposing: true);
//        GC.SuppressFinalize(this);
//    }

//    /// <summary>
//    /// 释放资源.
//    /// </summary>
//    /// <param name="disposing"></param>
//    protected virtual void Dispose(bool disposing)
//    {
//        if (!disposedValue)
//        {
//            if (disposing)
//            {
//                _s3Client.Dispose();
//            }

//            disposedValue = true;
//        }
//    }
//}
