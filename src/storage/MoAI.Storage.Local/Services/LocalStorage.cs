using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Store.Services;
using System.Net;

namespace MoAI.Storage.Services;

/// <summary>
/// 本地文件存储.
/// </summary>
public class LocalStorage : IStorage
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStorage"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public LocalStorage(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task DeleteFilesAsync(IEnumerable<string> objectKeys)
    {
        await Task.CompletedTask;
        foreach (var item in objectKeys)
        {
            var filePath = Path.Combine(_systemOptions.Storage.LocalPath, item);
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
        var sourceFilePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"文件 {objectKey} 不存在.");
        }

        File.Copy(sourceFilePath, filePath, true);
    }

    /// <inheritdoc/>
    public Task<bool> FileExistsAsync(string objectKey)
    {
        var filePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);
        return File.Exists(filePath) ? Task.FromResult(true) : Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, bool>> FilesExistsAsync(IEnumerable<string> objectKeys)
    {
        await Task.CompletedTask;
        Dictionary<string, bool> results = new();
        foreach (var objectKey in objectKeys)
        {
            var filePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);
            results[objectKey] = File.Exists(filePath);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePreSignedUploadUrlAsync(FileObject fileObject)
    {
        await Task.CompletedTask;

        // 生成预上传地址
        var expiry = DateTimeOffset.Now.Add(fileObject.ExpiryDuration).ToUnixTimeMilliseconds();
        var token = $"{fileObject.ObjectKey}|{expiry}|{fileObject.MaxFileSize}|{fileObject.ContentType}";
        var tokenEncode = HashHelper.ComputeSha256Hash(token);
        var objectPath = WebUtility.UrlEncode(fileObject.ObjectKey);

        var uploadUrl = new Uri(new Uri(_systemOptions.Server), $"/api/storage/upload/{objectPath}?expiry={expiry}&size={fileObject.MaxFileSize}&token={tokenEncode}").ToString();
        return uploadUrl;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, long>> GetFileSizeAsync(IEnumerable<string> objectKeys)
    {
        await Task.CompletedTask;
        Dictionary<string, long> results = new();
        foreach (var objectKey in objectKeys)
        {
            var filePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);
            var fileInfo = new FileInfo(filePath);
            results[objectKey] = fileInfo.Exists ? fileInfo.Length : 0;
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<long> GetFileSizeAsync(string objectKey)
    {
        await Task.CompletedTask;
        var filePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);
        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Exists)
        {
            return fileInfo.Length;
        }

        return 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, Uri>> GetFilesUrlAsync(IReadOnlyCollection<KeyValueString> objectKeys, TimeSpan expiryDuration)
    {
        await Task.CompletedTask;
        Dictionary<string, Uri> results = new();
        foreach (var objectKey in objectKeys)
        {
            var objectPath = WebUtility.UrlEncode(objectKey.Key);

            var expiry = DateTimeOffset.Now.Add(expiryDuration).ToUnixTimeMilliseconds();
            var token = HashHelper.ComputeSha256Hash($"{expiry}|{objectKey.Key}|{objectKey.Value}");
            results[objectKey.Key] = new Uri(new Uri(_systemOptions.Server), relativeUri: $"/api/download/{objectKey.Value}?key={objectPath}&expiry={expiry}&token={token}");
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<Uri> GetFileUrlAsync(string objectKey, string fileName, TimeSpan expiryDuration)
    {
        await Task.CompletedTask;
        var objectPath = WebUtility.UrlEncode(objectKey);
        var expiry = DateTimeOffset.Now.Add(expiryDuration).ToUnixTimeMilliseconds();
        var token = HashHelper.ComputeSha256Hash($"{expiry}|{objectKey}|{fileName}");
        return new Uri(new Uri(_systemOptions.Server), relativeUri: $"/download/{fileName}?key={objectPath}&expiry={expiry}&token={token}");
    }

    /// <inheritdoc/>
    public async Task UploadFileAsync(Stream inputStream, string objectKey)
    {
        await Task.CompletedTask;

        var targetFilePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);
        var targetFileInfo = new FileInfo(targetFilePath);
        if (targetFileInfo.Exists)
        {
            targetFileInfo.Delete();
        }

        var dir = Directory.GetParent(targetFilePath)!;
        if (!dir.Exists)
        {
            dir.Create();
        }

        using var fileStream = new FileStream(targetFilePath, FileMode.CreateNew, FileAccess.Write);
        await inputStream.CopyToAsync(fileStream);
    }
}
