#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Paddleocr;
using MoAI.Infra.Paddleocr.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins.Paddleocr.Common;
using System.ComponentModel;
using System.Text.Json;

namespace MoAI.Plugin.Plugins.Paddleocr.Vl;

/// <summary>
/// Paddleocr 文档解析插件 (PaddleOCR-VL).
/// </summary>
[NativePluginConfig(
    "paddleocr_vl",
    Name = "Paddleocr 文档解析 (VL)",
    Description = "使用 PaddleOCR-VL 视觉语言模型进行文档解析",
    Classify = NativePluginClassify.OCR,
    ConfigType = typeof(PaddleocrPluginConfig))]
[InjectOnTransient]
[Description("使用 PaddleOCR-VL 视觉语言模型进行文档解析")]
public partial class PaddleocrVlPlugin : INativePluginRuntime
{
    private readonly IPaddleocrClient _paddleocrClient;
    private PaddleocrPluginConfig _config = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleocrVlPlugin"/> class.
    /// </summary>
    /// <param name="paddleocrClient">Paddleocr 客户端。</param>
    public PaddleocrVlPlugin(IPaddleocrClient paddleocrClient)
    {
        _paddleocrClient = paddleocrClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new PaddleOcrVlRequest
        {
            File = "https://example.com/document.pdf",
            FileType = 0,
            UseDocOrientationClassify = true,
            UseLayoutDetection = true,
            UseChartRecognition = true,
            PrettifyMarkdown = true
        };

        return JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <inheritdoc/>
    public Task<string?> CheckConfigAsync(string config)
    {
        try
        {
            var objectParams = JsonSerializer.Deserialize<PaddleocrPluginConfig>(config);
            if (string.IsNullOrWhiteSpace(objectParams?.ApiUrl))
            {
                return Task.FromResult<string?>("API 地址不能为空。");
            }

            return Task.FromResult<string?>(string.Empty);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"参数解析失败: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task ImportConfigAsync(string config)
    {
        _config = JsonSerializer.Deserialize<PaddleocrPluginConfig>(config) ?? new PaddleocrPluginConfig();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var testParams = JsonSerializer.Deserialize<PaddleOcrVlRequest>(@params)!;
            var result = await InvokeAsync(
                testParams.File,
                testParams.FileType,
                testParams.UseDocOrientationClassify,
                testParams.UseDocUnwarping,
                testParams.UseLayoutDetection,
                testParams.UseChartRecognition,
                testParams.PrettifyMarkdown,
                testParams.ShowFormulaNumber);
            return JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <summary>
    /// 执行 PaddleOCR-VL 文档解析。
    /// </summary>
    /// <param name="file">文档文件URL或Base64编码内容。</param>
    /// <param name="fileType">文件类型 (0=PDF, 1=图像)。</param>
    /// <param name="useDocOrientationClassify">是否使用文档方向分类。</param>
    /// <param name="useDocUnwarping">是否使用文本图像矫正。</param>
    /// <param name="useLayoutDetection">是否使用版面区域检测排序。</param>
    /// <param name="useChartRecognition">是否使用图表解析。</param>
    /// <param name="prettifyMarkdown">是否输出美化后的 Markdown。</param>
    /// <param name="showFormulaNumber">Markdown 中是否包含公式编号。</param>
    /// <returns>文档解析结果。</returns>
    [KernelFunction("invoke")]
    [Description("执行 PaddleOCR-VL 文档解析")]
    public async Task<PaddleLayoutParsingResult> InvokeAsync(
        [Description("文档文件URL或Base64编码内容")] string file,
        [Description("文件类型 (0=PDF, 1=图像)")] int? fileType = null,
        [Description("是否使用文档方向分类")] bool? useDocOrientationClassify = null,
        [Description("是否使用文本图像矫正")] bool? useDocUnwarping = null,
        [Description("是否使用版面区域检测排序")] bool? useLayoutDetection = null,
        [Description("是否使用图表解析")] bool? useChartRecognition = null,
        [Description("是否输出美化后的 Markdown")] bool? prettifyMarkdown = null,
        [Description("Markdown 中是否包含公式编号")] bool? showFormulaNumber = null)
    {
        if (!string.IsNullOrWhiteSpace(_config?.ApiUrl))
        {
            _paddleocrClient.Client.BaseAddress = new Uri(_config.ApiUrl);
        }

        var request = new PaddleOcrVlRequest
        {
            File = file,
            FileType = fileType,
            UseDocOrientationClassify = useDocOrientationClassify,
            UseDocUnwarping = useDocUnwarping,
            UseLayoutDetection = useLayoutDetection,
            UseChartRecognition = useChartRecognition,
            PrettifyMarkdown = prettifyMarkdown,
            ShowFormulaNumber = showFormulaNumber
        };

        var response = await _paddleocrClient.LayoutParsingVlAsync(_config?.Token ?? string.Empty, request);
        HandleApiError(response);

        return response.Result!;
    }

    private static void HandleApiError<T>(MoAI.Infra.Paddleocr.PaddleOcrResponse<T> response)
    {
        if (response.ErrorCode != 0)
        {
            throw new BusinessException($"Paddleocr API 错误: {response.ErrorMsg}");
        }
    }
}
