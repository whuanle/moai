#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using MediatR;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Paddleocr;
using MoAI.Infra.Paddleocr.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Paddleocr.Common;
using MoAI.Plugin.Paddleocr.Ocr;
using MoAI.Plugin.Plugins.Paddleocr.Common;
using MoAI.Plugin.Plugins.Paddleocr.Ocr;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MoAI.Plugin.Plugins.Paddleocr.StructureV3;

/// <summary>
/// Paddleocr 文档解析插件 (PP-StructureV3).
/// </summary>
[NativePluginConfig(
    "paddleocr_structure_v3",
    Name = "Paddleocr 文档解析 (StructureV3)",
    Description = "使用 Paddleocr PP-StructureV3 模型进行文档版面分析和解析，适合各类带有结构的复杂文档，例如表格、发票、公式，速度比较慢，建议只处理单页文件",
    Classify = NativePluginClassify.OCR,
    ConfigType = typeof(PaddleocrPluginConfig),
    ParamType = typeof(PaddleocrStructureV3Params))]
[InjectOnTransient]
[Description("使用 Paddleocr PP-StructureV3 模型进行文档版面分析和解析，适合各类带有结构的复杂文档，例如表格、发票、公式，速度比较慢，建议只处理单页文件")]
public partial class PaddleocrStructureV3Plugin : PaddleocrPluginBase, INativePluginRuntime, IPaddleocrPlugin
{
    private readonly IPaddleocrClient _paddleocrClient;
    private PaddleocrPluginConfig _config = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleocrStructureV3Plugin"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="paddleocrClient"></param>
    public PaddleocrStructureV3Plugin(IMediator mediator, IServiceProvider serviceProvider, IPaddleocrClient paddleocrClient)
        : base(serviceProvider, mediator)
    {
        _paddleocrClient = paddleocrClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "file": "https://example.com/document.pdf",  // 文档文件URL或Base64编码内容
              "fileType": 0,                                   // 文件类型 (0=PDF, 1=图像)
              "useDocOrientationClassify": true,               // 是否使用文档方向分类
              "useDocUnwarping": false,                        // 是否使用文本图像矫正
              "useTableRecognition": false,                     // 是否使用表格识别
              "useFormulaRecognition": false,                   // 是否使用公式识别
              "useSealRecognition": false,                     // 是否使用印章识别
              "useChartRecognition": false,                    // 是否使用图表解析
              "useRegionDetection": false                      // 是否使用文档区域检测
            }
            """;
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
    public async Task<(IReadOnlyCollection<string> Texts, IReadOnlyCollection<string> Images)> OcrAsync(string base64, string @params)
    {
        try
        {
            var testParams = JsonSerializer.Deserialize<PaddleocrStructureV3Params>(@params, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping)!;
            if (!string.IsNullOrWhiteSpace(_config?.ApiUrl))
            {
                _paddleocrClient.Client.BaseAddress = new Uri(_config.ApiUrl);
            }

            var request = new PaddleLayoutParsingRequest
            {
                File = base64,
                FileType = testParams.FileType,
                UseDocOrientationClassify = testParams.UseDocOrientationClassify,
                UseDocUnwarping = testParams.UseDocUnwarping,
                UseTableRecognition = testParams.UseTableRecognition,
                UseFormulaRecognition = testParams.UseFormulaRecognition,
                UseSealRecognition = testParams.UseSealRecognition,
                UseChartRecognition = testParams.UseChartRecognition,
                UseRegionDetection = testParams.UseRegionDetection
            };

            var response = await _paddleocrClient.StructureV3Async($"token {_config?.Token}", request);
            HandleApiError(response);

            List<string> texts = new();
            List<string> images = new();
            StringBuilder stringBuilder = new();
            foreach (var ocrResult in response.Result!.OcrResults!)
            {
                stringBuilder.Clear();

                var prunedResult = ocrResult.GetProperty("prunedResult");

                var recTexts = prunedResult.GetProperty("rec_texts");
                var imageUrl = ocrResult.GetProperty("ocrImage").ToString();

                await UploadTempFileAsync(images, imageUrl);

                foreach (var text in recTexts.EnumerateArray())
                {
                    stringBuilder.AppendLine(text.GetString()!);
                }

                texts.Add(stringBuilder.ToString());
            }

            return (texts, images);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var testParams = JsonSerializer.Deserialize<PaddleocrStructureV3Params>(@params, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping)!;
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
    public async Task<string> InvokeAsync(
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

        var response = await _paddleocrClient.StructureV3Async($"token {_config?.Token}", request);
        HandleApiError(response);

        StringBuilder stringBuilder = new();
        foreach (var ocrResult in response.Result!.LayoutParsingResults!)
        {
            foreach (var item in ocrResult.PrunedResult?.GetProperty("seal_res_list").EnumerateArray())
            {
                var text = item.GetProperty("rec_texts").EnumerateArray().FirstOrDefault();
                stringBuilder.AppendLine(text.ToString());
            }
        }

        return stringBuilder.ToString()!;
    }

    private static void HandleApiError<T>(MoAI.Infra.Paddleocr.PaddleOcrResponse<T> response)
    {
        if (response.ErrorCode != 0)
        {
            throw new BusinessException($"Paddleocr API 错误: {response.ErrorMsg}");
        }
    }
}
