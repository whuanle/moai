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

namespace MoAI.Plugin.Plugins.Paddleocr.StructureV3;

/// <summary>
/// Paddleocr 文档解析插件 (PP-StructureV3).
/// </summary>
[NativePluginConfig(
    "paddleocr_structure_v3",
    Name = "Paddleocr 文档解析 (StructureV3)",
    Description = "使用 Paddleocr PP-StructureV3 模型进行文档版面分析和解析",
    Classify = NativePluginClassify.OCR,
    ConfigType = typeof(PaddleocrPluginConfig))]
[InjectOnTransient]
[Description("使用 Paddleocr PP-StructureV3 模型进行文档版面分析和解析")]
public partial class PaddleocrStructureV3Plugin : INativePluginRuntime
{
    private readonly IPaddleocrClient _paddleocrClient;
    private PaddleocrPluginConfig _config = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleocrStructureV3Plugin"/> class.
    /// </summary>
    /// <param name="paddleocrClient">Paddleocr 客户端。</param>
    public PaddleocrStructureV3Plugin(IPaddleocrClient paddleocrClient)
    {
        _paddleocrClient = paddleocrClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new PaddleLayoutParsingRequest
        {
            File = "https://example.com/document.pdf",
            FileType = 0,
            UseDocOrientationClassify = true,
            UseTableRecognition = true,
            UseFormulaRecognition = true
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
            var testParams = JsonSerializer.Deserialize<PaddleLayoutParsingRequest>(@params)!;
            var result = await InvokeAsync(
                testParams.File,
                testParams.FileType,
                testParams.UseDocOrientationClassify,
                testParams.UseDocUnwarping,
                testParams.UseTableRecognition,
                testParams.UseFormulaRecognition,
                testParams.UseSealRecognition,
                testParams.UseChartRecognition,
                testParams.UseRegionDetection);
            return JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <summary>
    /// 执行 Paddleocr 文档解析 (PP-StructureV3)。
    /// </summary>
    /// <param name="file">文档文件URL或Base64编码内容。</param>
    /// <param name="fileType">文件类型 (0=PDF, 1=图像)。</param>
    /// <param name="useDocOrientationClassify">是否使用文档方向分类。</param>
    /// <param name="useDocUnwarping">是否使用文本图像矫正。</param>
    /// <param name="useTableRecognition">是否使用表格识别。</param>
    /// <param name="useFormulaRecognition">是否使用公式识别。</param>
    /// <param name="useSealRecognition">是否使用印章识别。</param>
    /// <param name="useChartRecognition">是否使用图表解析。</param>
    /// <param name="useRegionDetection">是否使用文档区域检测。</param>
    /// <returns>文档解析结果。</returns>
    [KernelFunction("invoke")]
    [Description("执行 Paddleocr 文档解析 (PP-StructureV3)")]
    public async Task<PaddleLayoutParsingResult> InvokeAsync(
        [Description("文档文件URL或Base64编码内容")] string file,
        [Description("文件类型 (0=PDF, 1=图像)")] int? fileType = null,
        [Description("是否使用文档方向分类")] bool? useDocOrientationClassify = null,
        [Description("是否使用文本图像矫正")] bool? useDocUnwarping = null,
        [Description("是否使用表格识别")] bool? useTableRecognition = null,
        [Description("是否使用公式识别")] bool? useFormulaRecognition = null,
        [Description("是否使用印章识别")] bool? useSealRecognition = null,
        [Description("是否使用图表解析")] bool? useChartRecognition = null,
        [Description("是否使用文档区域检测")] bool? useRegionDetection = null)
    {
        if (!string.IsNullOrWhiteSpace(_config?.ApiUrl))
        {
            _paddleocrClient.Client.BaseAddress = new Uri(_config.ApiUrl);
        }

        var request = new PaddleLayoutParsingRequest
        {
            File = file,
            FileType = fileType,
            UseDocOrientationClassify = useDocOrientationClassify,
            UseDocUnwarping = useDocUnwarping,
            UseTableRecognition = useTableRecognition,
            UseFormulaRecognition = useFormulaRecognition,
            UseSealRecognition = useSealRecognition,
            UseChartRecognition = useChartRecognition,
            UseRegionDetection = useRegionDetection
        };

        var response = await _paddleocrClient.StructureV3Async(_config?.Token ?? string.Empty, request);
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
