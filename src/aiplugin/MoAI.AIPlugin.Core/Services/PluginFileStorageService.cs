using System.Security.Cryptography;
using System.Text;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Put;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件文件存储领域服务默认实现：把插件产物落到底层文件存储，并按文件 id 提供下载/公开地址.
/// </summary>
public class PluginFileStorageService : IPluginFileStorageService
{
    /// <summary>
    /// 默认存储子目录.
    /// </summary>
    private const string DefaultPrefix = "plugins";

    private const int CopyBufferSize = 81920;

    /// <summary>
    /// 下载地址有效期；底层存储当前统一为一分钟，此处传入仅为兼容接口.
    /// </summary>
    private static readonly TimeSpan DownloadUrlExpiry = TimeSpan.FromMinutes(1);

    private readonly IStorageService _storageService;
    private readonly IPutClient _putClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginFileStorageService"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="putClient">外部 HTTP 客户端，用于下载公网文件.</param>
    public PluginFileStorageService(IStorageService storageService, IPutClient putClient)
    {
        _storageService = storageService;
        _putClient = putClient;
    }

    /// <inheritdoc/>
    public async Task<long> SaveAsync(Stream stream, PluginFileSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        if (stream.CanSeek)
        {
            return await SaveSeekableAsync(stream, options, cancellationToken).ConfigureAwait(false);
        }

        // 非可定位流（网络流等）先落临时文件：既保证能计算 SHA-256，又避免把大文件整个读进内存.
        var tempPath = Path.Combine(Path.GetTempPath(), $"moai-plugin-file-{Guid.CreateVersion7():N}.tmp");
        try
        {
            long size;
            await using (var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.Asynchronous))
            {
                await stream.CopyToAsync(temp, cancellationToken).ConfigureAwait(false);
                size = temp.Length;
            }

            await using var readStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.Asynchronous);
            var sha256 = await ComputeSha256Async(readStream, cancellationToken).ConfigureAwait(false);
            return await UploadAsync(readStream, size, sha256, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <inheritdoc/>
    public async Task<long> SaveAsync(byte[] content, PluginFileSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        using var stream = new MemoryStream(content, writable: false);
        return await SaveAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> SaveFromUrlAsync(string url, PluginFileSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(400, "Url 必须为合法的 http/https 文件地址");
        }

        HttpResponseMessage response;
        try
        {
            response = await _putClient.Client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new BusinessException(ex, "下载文件失败：{0}", ex.Message);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new BusinessException(400, $"下载文件失败：HTTP {(int)response.StatusCode}");
            }

            options ??= new PluginFileSaveOptions();
            var saveOptions = new PluginFileSaveOptions
            {
                FileName = ResolveFileName(uri, response, options.FileName),
                ContentType = string.IsNullOrWhiteSpace(options.ContentType)
                    ? response.Content.Headers.ContentType?.MediaType
                    : options.ContentType,
                IsPublic = options.IsPublic,
                Prefix = options.Prefix,
            };

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await SaveAsync(stream, saveOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<PluginStoredFile?> GetFileInfoAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var fileInfo = await _storageService.GetFileInfoByIdAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (fileInfo == null)
        {
            return null;
        }

        var isPublic = FileStoreHelper.IsPublicObjectKey(fileInfo.ObjectKey);
        return new PluginStoredFile
        {
            FileId = fileInfo.FileId,
            FileExtension = fileInfo.FileExtension,
            ContentType = fileInfo.ContentType,
            FileSize = fileInfo.FileSize,
            FileSha256 = fileInfo.FileSha256,
            IsPublic = isPublic,
            PublicUrl = isPublic ? _storageService.GetPublicFileUrl(fileInfo.ObjectKey) : null,
        };
    }

    /// <inheritdoc/>
    public async Task<Uri> GetDownloadUrlAsync(long fileId, string? fileName = null, CancellationToken cancellationToken = default)
    {
        var fileInfo = await _storageService.GetFileInfoByIdAsync(fileId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException(404, "文件不存在");

        var downloadName = string.IsNullOrWhiteSpace(fileName)
            ? $"file-{fileId}{fileInfo.FileExtension}"
            : NormalizeFileName(fileName);

        return await _storageService
            .GetDownloadUrlAsync(fileInfo.ObjectKey, downloadName, DownloadUrlExpiry, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Uri?> GetPublicUrlAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var fileInfo = await _storageService.GetFileInfoByIdAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (fileInfo == null || !FileStoreHelper.IsPublicObjectKey(fileInfo.ObjectKey))
        {
            return null;
        }

        return _storageService.GetPublicFileUrl(fileInfo.ObjectKey);
    }

    private async Task<long> SaveSeekableAsync(Stream stream, PluginFileSaveOptions options, CancellationToken cancellationToken)
    {
        var startPosition = stream.Position;
        var size = stream.Length - startPosition;
        var sha256 = await ComputeSha256Async(stream, cancellationToken).ConfigureAwait(false);
        stream.Position = startPosition;

        return await UploadAsync(stream, size, sha256, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> UploadAsync(Stream stream, long size, string sha256, PluginFileSaveOptions options, CancellationToken cancellationToken)
    {
        if (size > int.MaxValue)
        {
            throw new BusinessException(413, "文件过大，单个文件不能超过 2GB");
        }

        var fileName = NormalizeFileName(options.FileName);
        var contentType = ResolveContentType(options.ContentType, fileName);
        var objectKey = BuildObjectKey(sha256, fileName, options);

        var result = await _storageService.UploadStreamAsync(new UploadStreamFileCommand
        {
            FileStream = stream,
            ContentType = contentType,
            FileSize = (int)size,
            SHA256 = sha256,
            ObjectKey = objectKey,
        }, cancellationToken).ConfigureAwait(false);

        return result.FileId;
    }

    /// <summary>
    /// 计算流内容的 SHA-256；读取后把流位置恢复到调用前.
    /// </summary>
    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        var startPosition = stream.Position;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        stream.Position = startPosition;
        return Convert.ToHexStringLower(hash);
    }

    private static string BuildObjectKey(string sha256, string fileName, PluginFileSaveOptions options)
    {
        var prefix = NormalizePrefix(options.Prefix);
        var extension = NormalizeExtension(fileName);
        var objectKey = $"{prefix}/{sha256}{extension}";

        return options.IsPublic ? FileStoreHelper.ToPublicObjectKey(objectKey) : objectKey;
    }

    private static string ResolveContentType(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return contentType.Trim();
        }

        return FileStoreHelper.GetMimeType(Path.GetExtension(fileName), "application/octet-stream");
    }

    private static string NormalizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        return string.IsNullOrEmpty(name) ? "file" : name;
    }

    private static string NormalizeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length <= 1)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(".");
        foreach (var c in extension[1..])
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.Length <= 1 ? string.Empty : builder.ToString();
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return DefaultPrefix;
        }

        // 公开前缀只由 IsPublic 决定，避免插件通过 Prefix 绕过公开/私有约定
        var segments = prefix
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x != "." && x != "..")
            .Select(x => string.Concat(x.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            .Where(x => x.Length > 0 && !string.Equals(x, FileStoreHelper.PublicPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return segments.Length == 0 ? DefaultPrefix : string.Join('/', segments);
    }

    private static string ResolveFileName(Uri uri, HttpResponseMessage response, string? requestedFileName)
    {
        if (!string.IsNullOrWhiteSpace(requestedFileName))
        {
            return NormalizeFileName(requestedFileName);
        }

        var dispositionName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        var headerName = NormalizeFileName(dispositionName?.Trim('"'));
        if (!string.Equals(headerName, "file", StringComparison.Ordinal))
        {
            return headerName;
        }

        var urlName = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .LastOrDefault();

        return NormalizeFileName(urlName);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
#pragma warning disable CA1031 // 临时文件清理失败不应影响主流程
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
