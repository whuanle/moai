// <copyright file="LocalPrivateStorage.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra;
using MoAI.Infra.Service;
using MoAI.Store.Services;

namespace MoAI.Storage.Services;

/// <summary>
/// LocalPrivateStorage.
/// </summary>
public class LocalPrivateStorage : IPrivateFileStorage
{
    private readonly SystemOptions _systemOptions;
    private readonly string _localPath;
    private readonly IAESProvider _aesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalPrivateStorage"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    /// <param name="aesProvider"></param>
    public LocalPrivateStorage(SystemOptions systemOptions, IAESProvider aesProvider)
    {
        _systemOptions = systemOptions;
        _localPath = Path.Combine(systemOptions.Storage.FilePath, "contents");
        _aesProvider = aesProvider;
    }

    /// <inheritdoc/>
    public async Task DeleteFilesAsync(IEnumerable<string> objectKeys)
    {
        await Task.CompletedTask;

        foreach (var objectKey in objectKeys)
        {
            var filePath = Path.Combine(_localPath, objectKey);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(string objectKey, string filePath)
    {
        await Task.CompletedTask;

        var sourceFilePath = Path.Combine(_localPath, objectKey);
        File.Copy(sourceFilePath, filePath, true);
    }

    /// <inheritdoc/>
    public async Task<bool> FileExistsAsync(string objectKey)
    {
        await Task.CompletedTask;
        var filePath = Path.Combine(_localPath, objectKey);
        return File.Exists(filePath);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, bool>> FilesExistsAsync(IEnumerable<string> objectKeys)
    {
        await Task.CompletedTask;

        var results = new Dictionary<string, bool>();
        foreach (var objectKey in objectKeys)
        {
            var filePath = Path.Combine(_localPath, objectKey);
            results[objectKey] = File.Exists(filePath);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePreSignedUploadUrlAsync(FileObject fileObject)
    {
        await Task.CompletedTask;

        var objectKey = fileObject.ObjectKey;
        var expires = (int)fileObject.ExpiryDuration.TotalSeconds;
        var date = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var size = fileObject.MaxFileSize;
        var contentType = fileObject.ContentType;

        var token = $"{objectKey}|{expires}|{date}|{size}|{contentType}";
        var tokenEncode = _aesProvider.Encrypt(token);

        return new Uri(new Uri(_systemOptions.Server), $"/api/storage/upload/private/{fileObject.ObjectKey}?token={tokenEncode}&expires={expires}&date={date}&size={size}").ToString();
    }

    /// <inheritdoc/>
    public async Task<long> GetFileSizeAsync(string objectKey)
    {
        await Task.CompletedTask;
        var filePath = Path.Combine(_localPath, objectKey);
        if (File.Exists(filePath))
        {
            return new FileInfo(filePath).Length;
        }

        return 0L;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, long>> GetFileSizeAsync(IEnumerable<string> objectKeys)
    {
        await Task.CompletedTask;

        var results = new Dictionary<string, long>();
        foreach (var item in results)
        {
            var filePath = Path.Combine(_localPath, item.Key);
            if (File.Exists(filePath))
            {
                results[item.Key] = new FileInfo(filePath).Length;
            }
            else
            {
                results[item.Key] = 0L;
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<Uri> GetFileUrlAsync(string objectKey, TimeSpan expiryDuration)
    {
        await Task.CompletedTask;

        // 本地存储不支持预签名 URL，直接返回文件路径
        var filePath = Path.Combine(_localPath, objectKey);
        if (File.Exists(filePath))
        {
            return new Uri(new Uri(_systemOptions.Server), $"/api/download/private/{objectKey}");
        }

        throw new FileNotFoundException($"File not found: {objectKey}");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, Uri>> GetFileUrlAsync(IEnumerable<string> objectKeys, TimeSpan expiryDuration)
    {
        await Task.CompletedTask;

        var results = new Dictionary<string, Uri>();
        foreach (var objectKey in objectKeys)
        {
            var filePath = Path.Combine(_localPath, objectKey);
            if (File.Exists(filePath))
            {
                results[objectKey] = new Uri(new Uri(_systemOptions.Server), $"/api/download/private/{objectKey}");
            }
            else
            {
                results[objectKey] = null; // 或者抛出异常，取决于你的需求
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task UploadFileAsync(Stream inputStream, string objectKey)
    {
        var filePath = Path.Combine(_localPath, objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await inputStream.CopyToAsync(fileStream);
        }
    }
}
