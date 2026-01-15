#pragma warning disable CA1054 // 类 URI 参数不应为字符串
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Handlers;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.TextExtract;

/// <summary>
/// 文本提取插件.
/// </summary>
[Attributes.NativePluginConfig(
    "text_extract",
    Name = "文本提取",
    Description = "支持格式：文本(.txt .md)，网页(.html .xml)，代码(.js .css .sh)，文档(.pdf .docx .pptx .xlsx .epub .odt)，数据(.json .csv)，压缩包(.zip .rar .7z .tar .gz)",
#pragma warning restore SA1118 // Parameter should not span multiple lines
    Classify = NativePluginClassify.DocumentProcessing)]
[Description("从文件中提取文本内容")]
[InjectOnTransient]
public class TextExtractPlugin : IToolPluginRuntime
{
    private readonly KmTextExtractionHandler _textExtractionHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextExtractPlugin"/> class.
    /// </summary>
    /// <param name="textExtractionHandler"></param>
    public TextExtractPlugin(KmTextExtractionHandler textExtractionHandler)
    {
        _textExtractionHandler = textExtractionHandler;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        string example = "https://example.com/file.pdf";
        return System.Text.Json.JsonSerializer.Serialize(
            new TextExtractPluginParam
            {
                FileName = "test.pdf",
                Url = example
            },
            JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 从文件中提取文本内容。
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="url">文件路径。</param>
    /// <returns>提取的文本内容。</returns>
    [KernelFunction("invoke")]
    [Description("从文件中提取文本内容，支持pdf、md、word、html等文件")]
    public async Task<string> InvokeAsync([Description("带文件后缀，如 test.pdf/test.docx/test.html")] string fileName, [Description("文件地址")] string url)
    {
        try
        {
            var text = await _textExtractionHandler.ExtractUrlAsync(new Uri(url), fileName);
            return text;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"文本提取失败: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var param = System.Text.Json.JsonSerializer.Deserialize<TextExtractPluginParam>(@params);
            var result = await InvokeAsync(param!.FileName, param.Url);
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }
}