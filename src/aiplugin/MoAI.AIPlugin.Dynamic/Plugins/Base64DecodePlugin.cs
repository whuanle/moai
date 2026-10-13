using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Base64 解码（动态插件）：把 Base64 字符串解码为文本或二进制.
/// </summary>
/// <remarks>
/// 内容形态三分：严格 UTF-8 文本直接回填 <c>Text</c>（超长按配置截断）；
/// 图片（PNG/JPEG/GIF/WebP/BMP/ICO/TIFF 魔数与 SVG 文本）转存文件存储并返回公开地址与 Markdown 引用，可直接在对话中渲染；
/// 其它二进制同样转存并返回下载链接。输入支持 data URL 前缀（<c>data:image/png;base64,xxxx</c>），允许含空白与换行。
/// 转存走 <see cref="IStorageService"/>，按 SHA-256 内容寻址幂等去重。
/// </remarks>
[AiPlugin(
    key: "base64_decode",
    Name = "Base64 解码",
    Description = "把 Base64 字符串解码为文本、图片或文件：文本直接返回，图片/二进制转存为可访问地址（图片附 Markdown 引用，可直接在对话中展示）；支持 data URL 前缀")]
public class Base64DecodePlugin : IDynamicPluginRuntime<Base64DecodeRequest, Base64DecodeResponse, Base64DecodeConfig>
{
    /// <summary>解码结果字节数下限.</summary>
    private const int MinMaxDecodeBytes = 1024;

    /// <summary>解码结果字节数上限（32MB，防御性硬顶）.</summary>
    private const int MaxMaxDecodeBytes = 32 * 1024 * 1024;

    /// <summary>文本输出字符数下限.</summary>
    private const int MinTextMaxLength = 100;

    /// <summary>文本输出字符数上限.</summary>
    private const int MaxTextMaxLength = 200000;

    private readonly IStorageService _storageService;
    private Base64DecodeConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Base64DecodePlugin"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务，用于把图片/二进制转存为公开可访问文件.</param>
    public Base64DecodePlugin(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Base64": "SGVsbG8sIE1vQUkh", // 待解码内容；允许含空白换行，也支持 data:image/png;base64,xxxx 形式
              "FileName": "hello.txt"       // 可选：文件名，仅用于推断转存扩展名与结果回显
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "MaxDecodeBytes": 2097152, // 单次解码最大字节数，默认 2MB
              "TextMaxLength": 20000     // 文本输出最大字符数，超出截断
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(Base64DecodeConfig config)
    {
        _config = new Base64DecodeConfig
        {
            MaxDecodeBytes = Math.Clamp(config.MaxDecodeBytes, MinMaxDecodeBytes, MaxMaxDecodeBytes),
            TextMaxLength = Math.Clamp(config.TextMaxLength, MinTextMaxLength, MaxTextMaxLength),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<Base64DecodeResponse> RunAsync(Base64DecodeRequest request, CancellationToken cancellationToken)
    {
        var input = request.Base64?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            throw new BusinessException(400, "Base64 不能为空") { StatusCode = 400 };
        }

        // data URL 前缀剥离：data:<mime>;base64,<载荷>；前缀长度忽略不计，不影响后面的粗估。
        Base64ContentDetector.TryStripDataUrlPrefix(input, out var mimeTypeHint, out var payload);
        if (payload.Length > 0)
        {
            input = payload;
        }

        // 解码前粗估上界（原始长度必然 ≥ 有效字符数，按 4:3 折算不会低估），避免超大输入先占内存。
        if (input.Length / 4 * 3 > _config.MaxDecodeBytes)
        {
            throw new BusinessException(400, $"解码结果超过上限 {_config.MaxDecodeBytes} 字节，可在实例配置调大 MaxDecodeBytes") { StatusCode = 400 };
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input);
        }
        catch (FormatException)
        {
            throw new BusinessException(400, "输入不是有效的 Base64 字符串（允许含空白与换行，也支持 data:xxx;base64, 形式）") { StatusCode = 400 };
        }

        if (bytes.Length == 0)
        {
            throw new BusinessException(400, "解码结果为空") { StatusCode = 400 };
        }

        if (bytes.Length > _config.MaxDecodeBytes)
        {
            throw new BusinessException(400, $"解码结果 {bytes.Length} 字节超过上限 {_config.MaxDecodeBytes} 字节，可在实例配置调大 MaxDecodeBytes") { StatusCode = 400 };
        }

        // 形态判定顺序：文本（含 SVG 特判）→ 图片魔数 → 其它二进制。
        if (Base64ContentDetector.IsUtf8Text(bytes, out var text))
        {
            if (Base64ContentDetector.IsSvgText(text))
            {
                return await StoreAsFileAsync(bytes, "image/svg+xml", ".svg", request.FileName, cancellationToken).ConfigureAwait(false);
            }

            var truncated = text.Length > _config.TextMaxLength;
            return new Base64DecodeResponse
            {
                Kind = "text",
                ContentType = "text/plain",
                SizeBytes = bytes.Length,
                Text = truncated ? text[.._config.TextMaxLength] : text,
                TextTruncated = truncated,
                FileName = ResolveFileName(request.FileName, ".txt"),
            };
        }

        if (Base64ContentDetector.IsImageData(bytes, out var imageContentType, out var imageExtension))
        {
            return await StoreAsFileAsync(bytes, imageContentType, imageExtension, request.FileName, cancellationToken).ConfigureAwait(false);
        }

        // 其它二进制：有 data URL 类型声明时信任声明（如 application/pdf），否则按通用二进制。
        var contentType = string.IsNullOrEmpty(mimeTypeHint) ? "application/octet-stream" : mimeTypeHint;
        return await StoreAsFileAsync(bytes, contentType, ResolveBinaryExtension(contentType), request.FileName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把解码内容转存为公开可访问文件并组装响应.
    /// </summary>
    /// <param name="bytes">解码后的字节.</param>
    /// <param name="contentType">MIME 类型.</param>
    /// <param name="extension">扩展名（含点）.</param>
    /// <param name="requestFileName">请求提供的文件名.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>带公开地址与 Markdown 引用的响应.</returns>
    private async Task<Base64DecodeResponse> StoreAsFileAsync(byte[] bytes, string contentType, string extension, string requestFileName, CancellationToken cancellationToken)
    {
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        // 请求文件名仅取其扩展名参与 ObjectKey；内容按 SHA-256 寻址，同内容天然幂等去重。
        var objectKey = FileStoreHelper.ToPublicObjectKey(
            FileStoreHelper.GetObjectKey(sha256, $"decoded{extension}", isImage ? "images" : "files"));

        using var fileStream = new MemoryStream(bytes);
        await _storageService.UploadStreamAsync(new UploadStreamFileCommand
        {
            FileStream = fileStream,
            ContentType = contentType,
            FileSize = bytes.Length,
            SHA256 = sha256,
            ObjectKey = objectKey,
        }, cancellationToken).ConfigureAwait(false);

        var url = _storageService.GetPublicFileUrl(objectKey).ToString();
        return new Base64DecodeResponse
        {
            Kind = isImage ? "image" : "binary",
            ContentType = contentType,
            SizeBytes = bytes.Length,
            Url = url,
            Markdown = isImage ? $"![image]({url})" : $"[file]({url})",
            FileName = ResolveFileName(requestFileName, extension),
        };
    }

    /// <summary>
    /// 归一结果文件名：请求提供了带扩展名的名字就用它，否则按检测结果生成.
    /// </summary>
    /// <param name="requestFileName">请求提供的文件名.</param>
    /// <param name="fallbackExtension">兜底扩展名（含点）.</param>
    /// <returns>结果文件名.</returns>
    private static string ResolveFileName(string requestFileName, string fallbackExtension)
    {
        var name = requestFileName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return $"decoded{fallbackExtension}";
        }

        return Path.GetFileName(name);
    }

    /// <summary>
    /// 按内容类型推断二进制扩展名.
    /// </summary>
    /// <param name="contentType">MIME 类型.</param>
    /// <returns>扩展名（含点），未知返回 .bin.</returns>
    private static string ResolveBinaryExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/zip" => ".zip",
            "application/gzip" => ".gz",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/html" => ".html",
            "text/css" => ".css",
            "text/csv" => ".csv",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "video/mp4" => ".mp4",
            _ => ".bin"
        };
    }
}
