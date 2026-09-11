using System;
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
/// 文本提取（静态插件）：下载文件地址并按文件名后缀自动选择解析器提取文本.
/// </summary>
[AiPlugin(
    key: "static_text_extract",
    Name = "文本提取",
    Description = "从 http/https 文件地址提取文本，按文件名后缀支持 pdf/docx/pptx/xlsx/html/md/txt/json/csv 等格式")]
public class TextExtractPlugin : IStaticPluginRuntime<TextExtractRequest, TextExtractResponse>
{
    private readonly TextExtractionService _textExtractionService;
    private readonly IPutClient _putClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextExtractPlugin"/> class.
    /// </summary>
    /// <param name="textExtractionService">文本抽取服务（按文件后缀选择解析器）.</param>
    /// <param name="putClient">外部 HTTP 客户端，用于下载文件（复用统一的外部请求日志与遥测）.</param>
    public TextExtractPlugin(TextExtractionService textExtractionService, IPutClient putClient)
    {
        _textExtractionService = textExtractionService;
        _putClient = putClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "FileName": "test.pdf",                       // 带扩展名的文件名，用于识别格式
              "Url": "https://example.com/file.pdf"         // http/https 文件下载地址
            }
            """;
    }

    /// <inheritdoc/>
    public async Task<TextExtractResponse> RunAsync(TextExtractRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new BusinessException(400, "FileName 不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(400, "Url 必须为合法的 http/https 文件地址");
        }

        try
        {
            await using var stream = await _putClient.Client.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
            var text = await _textExtractionService.ExtractAsync(stream, request.FileName, cancellationToken).ConfigureAwait(false);
            return new TextExtractResponse { Text = text };
        }
        catch (BusinessException)
        {
            throw;
        }
#pragma warning disable CA1031 // 插件侧异常统一归一化为业务异常，错误消息透出底层提示便于排查
        catch (Exception ex)
        {
            throw new BusinessException(400, $"文本提取失败: {ex.Message}");
        }
#pragma warning restore CA1031
    }
}
