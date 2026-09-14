using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi.ToMarkdown;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Put;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 文件转 Markdown（静态插件）：下载 http/https 文件地址，按扩展名自动选择解析器转换为 Markdown.
/// 与 <see cref="TextExtractPlugin"/> 的差异：只需给文件地址即可运行（FileName 留空时从 Url 自动识别文件名），
/// 响应按 Markdown 语义返回并附带识别出的文件名与 MIME 类型.
/// </summary>
[AiPlugin(
    key: "static_file_to_markdown",
    Name = "文件转 Markdown",
    Description = "把 http/https 文件地址自动下载并转换为 Markdown，支持 pdf/docx/xlsx/pptx/html/md/txt/json 等格式；FileName 留空时从 Url 自动识别文件名")]
public class FileToMarkdownPlugin : IStaticPluginRuntime<FileToMarkdownRequest, FileToMarkdownResponse>
{
    private readonly MimeTypesDetection _mimeTypesDetection = new();
    private readonly TextExtractionService _textExtractionService;
    private readonly IPutClient _putClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileToMarkdownPlugin"/> class.
    /// </summary>
    /// <param name="textExtractionService">文本抽取服务（按文件后缀选择解析器，输出 Markdown）.</param>
    /// <param name="putClient">外部 HTTP 客户端，用于下载文件（复用统一的外部请求日志与遥测）.</param>
    public FileToMarkdownPlugin(TextExtractionService textExtractionService, IPutClient putClient)
    {
        _textExtractionService = textExtractionService;
        _putClient = putClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Url": "https://example.com/docs/report.pdf",  // http/https 文件下载地址
              "FileName": ""                                 // 可选：带扩展名的文件名；留空时从 Url 自动识别
            }
            """;
    }

    /// <inheritdoc/>
    public async Task<FileToMarkdownResponse> RunAsync(FileToMarkdownRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(400, "Url 必须为合法的 http/https 文件地址");
        }

        var fileName = ResolveFileName(request, uri);
        if (!_mimeTypesDetection.TryGetFileType(fileName, out var mimeType))
        {
            throw new BusinessException(400, $"不支持的文件类型: {Path.GetExtension(fileName)}（支持 pdf/docx/xlsx/pptx/html/md/txt/json 等）");
        }

        try
        {
            await using var stream = await _putClient.Client.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
            var markdown = await _textExtractionService.ExtractAsync(stream, fileName, cancellationToken).ConfigureAwait(false);
            return new FileToMarkdownResponse { FileName = fileName, FileType = mimeType ?? string.Empty, Markdown = markdown };
        }
        catch (BusinessException)
        {
            throw;
        }
#pragma warning disable CA1031 // 插件侧异常统一归一化为业务异常，错误消息透出底层提示便于排查
        catch (Exception ex)
        {
            throw new BusinessException(400, $"文件转 Markdown 失败: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// 解析文件名：优先用请求传入的 FileName（需带扩展名），否则从 Url 路径末段自动识别.
    /// </summary>
    /// <param name="request">请求参数.</param>
    /// <param name="uri">解析后的文件地址.</param>
    /// <returns>带扩展名的文件名.</returns>
    private static string ResolveFileName(FileToMarkdownRequest request, Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            var name = request.FileName.Trim();
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                throw new BusinessException(400, "FileName 需带扩展名，用于识别文件格式（如 report.pdf）");
            }

            return name;
        }

        var lastSegment = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(lastSegment) || string.IsNullOrEmpty(Path.GetExtension(lastSegment)))
        {
            throw new BusinessException(400, "无法从 Url 识别文件扩展名，请通过 FileName 传入带扩展名的文件名（如 report.pdf）");
        }

        return lastSegment;
    }
}
