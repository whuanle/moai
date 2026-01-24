#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using MediatR;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Paddleocr;
using MoAI.Infra.Paddleocr.Models;
using MoAI.Infra.Put;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Paddleocr.Common;
using MoAI.Plugin.Paddleocr.Ocr;
using MoAI.Plugin.Plugins.Paddleocr.Common;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MoAI.Plugin.Plugins.Paddleocr.Ocr;

/// <summary>
/// Paddleocr OCR 识别插件 (PP-OCRv5).
/// </summary>
[NativePluginConfig(
    "paddleocr_ocr",
    Name = "Paddleocr OCR 识别",
    Description = "使用 Paddleocr PP-OCRv5 模型进行图像/PDF文字识别，适合常规场景，速度较快，可以处理多页文件",
    Classify = NativePluginClassify.OCR,
    ConfigType = typeof(PaddleocrPluginConfig),
    ParamType = typeof(PaddleOcrParams))]
[InjectOnTransient]
[Description("使用 Paddleocr PP-OCRv5 模型进行图像/PDF文字识别，适合常规场景，速度较快，可以处理多页文件")]
public partial class PaddleocrPlugin : PaddleocrPluginBase, INativePluginRuntime, IPaddleocrPlugin
{
    private readonly IPaddleocrClient _paddleocrClient;

    private PaddleocrPluginConfig _config = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleocrPlugin"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="paddleocrClient"></param>
    public PaddleocrPlugin(IMediator mediator, IServiceProvider serviceProvider, IPaddleocrClient paddleocrClient)
        : base(serviceProvider, mediator)
    {
        _paddleocrClient = paddleocrClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "file": "https://example.com/document.pdf",   // 图像文件URL或Base64编码内容
              "fileType": 0,                                // 文件类型 (0=PDF, 1=图像)
              "useDocOrientationClassify": true,            // 是否使用文档方向分类
              "useDocUnwarping": false,                     // 是否使用文本图像矫正
              "useTextlineOrientation": false               // 是否使用文本行方向分类
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
            var testParams = JsonSerializer.Deserialize<PaddleOcrParams>(@params, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping)!;
            if (!string.IsNullOrWhiteSpace(_config?.ApiUrl))
            {
                _paddleocrClient.Client.BaseAddress = new Uri(_config.ApiUrl);
            }

            var request = new PaddleOcrRequest
            {
                File = base64,
                FileType = testParams.FileType,
                UseDocOrientationClassify = testParams.UseDocOrientationClassify,
                UseDocUnwarping = testParams.UseDocUnwarping,
                UseTextlineOrientation = testParams.UseTextlineOrientation
            };

            var response = await _paddleocrClient.OcrAsync($"token {_config?.Token}", request);
            HandleApiError(response);

            List<string> texts = new();
            List<string> images = new();
            StringBuilder stringBuilder = new();
            foreach (var ocrResult in response.Result!.OcrResults!)
            {
                stringBuilder.Clear();

                var recTexts = ocrResult.PrunedResult!.Value.GetProperty("rec_texts");

                if (string.IsNullOrWhiteSpace(ocrResult.OcrImage))
                {
                    continue;
                }

                await UploadTempFileAsync(images, ocrResult.OcrImage);

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
            var testParams = JsonSerializer.Deserialize<PaddleOcrParams>(@params, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping)!;
            var result = await InvokeAsync(
                testParams.File,
                testParams.FileType,
                testParams.UseDocOrientationClassify,
                testParams.UseDocUnwarping,
                testParams.UseTextlineOrientation);
            return JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <summary>
    /// 执行 Paddleocr OCR 识别。
    /// </summary>
    /// <param name="file">图像文件URL或Base64编码内容。</param>
    /// <param name="fileType">文件类型 (0=PDF, 1=图像)。</param>
    /// <param name="useDocOrientationClassify">是否使用文档方向分类。</param>
    /// <param name="useDocUnwarping">是否使用文本图像矫正。</param>
    /// <param name="useTextlineOrientation">是否使用文本行方向分类。</param>
    /// <returns>OCR 识别结果。</returns>
    [KernelFunction("invoke")]
    [Description("执行 Paddleocr OCR 识别")]
    public async Task<string> InvokeAsync(
        [Description("图像文件URL或Base64编码内容")] string file,
        [Description("文件类型 (0=PDF, 1=图像)")] int? fileType = null,
        [Description("是否使用文档方向分类")] bool? useDocOrientationClassify = null,
        [Description("是否使用文本图像矫正")] bool? useDocUnwarping = null,
        [Description("是否使用文本行方向分类")] bool? useTextlineOrientation = null)
    {
        if (!string.IsNullOrWhiteSpace(_config?.ApiUrl))
        {
            _paddleocrClient.Client.BaseAddress = new Uri(_config.ApiUrl);
        }

        var request = new PaddleOcrRequest
        {
            File = file,
            FileType = fileType,
            UseDocOrientationClassify = useDocOrientationClassify,
            UseDocUnwarping = useDocUnwarping,
            UseTextlineOrientation = useTextlineOrientation
        };

        var response = await _paddleocrClient.OcrAsync($"token {_config?.Token}", request);
        HandleApiError(response);

        StringBuilder stringBuilder = new();
        foreach (var ocrResult in response.Result!.OcrResults!)
        {
            var texts = ocrResult.PrunedResult!.Value.GetProperty("rec_texts");
            foreach (var text in texts.EnumerateArray())
            {
                stringBuilder.AppendLine(text.GetString());
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
