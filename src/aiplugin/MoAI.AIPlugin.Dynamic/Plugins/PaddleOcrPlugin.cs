using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using Refit;
using InfraPaddle = MoAI.Infra.Paddleocr;
using InfraPaddleModels = MoAI.Infra.Paddleocr.Models;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// PaddleOCR 通用文字识别（动态插件）：使用实例配置中的 API 地址与 Token 调用 PaddleOCR PP-OCRv5 接口进行图像/PDF 文字识别.
/// </summary>
/// <remarks>
/// 与旧 <c>NativePlugin.PaddleocrPlugin</c> 行为对齐：
/// <list type="bullet">
/// <item><description><see cref="InfraPaddle.IPaddleocrClient"/> 由基础设施层注册（Refit + <c>ExternalHttpMessageHandler</c>），插件内不新建 <c>HttpClient</c>；</description></item>
/// <item><description>API 地址来自实例配置 <see cref="PaddleOcrConfig.ApiUrl"/>，通过 <c>HttpClient.BaseAddress</c> 在每次执行前设置（Refit 客户端为 transient，每次插件执行独立 HttpClient）；</description></item>
/// <item><description>鉴权头按 PaddleOCR 协议拼 <c>token {Token}</c>（见 <see cref="PaddleOcrAuthorization"/>），未开启鉴权时为空字符串；</description></item>
/// <item><description>响应 <c>errorCode != 0</c> 归一为 <see cref="BusinessException"/>，由 <c>PluginExecutor</c> 转为运行结果失败.</description></item>
/// </list>
/// </remarks>
[AiPlugin(key: "paddleocr_ocr", Name = "PaddleOCR 文字识别", Description = "使用 PaddleOCR PP-OCRv5 模型对图像/PDF 进行文字识别，适合常规场景速度较快，支持多页文件")]
public class PaddleOcrPlugin : IDynamicPluginRuntime<PaddleOcrRequest, PaddleOcrResponse, PaddleOcrConfig>
{
    private readonly InfraPaddle.IPaddleocrClient _client;
    private PaddleOcrConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleOcrPlugin"/> class.
    /// </summary>
    /// <param name="client">PaddleOCR API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public PaddleOcrPlugin(InfraPaddle.IPaddleocrClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "File": "https://example.com/document.pdf", // 图像或 PDF 文件 URL；亦可直接填 Base64
              "FileType": 0,                                // 0=PDF，1=图像；缺省时由服务端推断
              "UseDocOrientationClassify": false,          // 是否使用文档方向分类
              "UseDocUnwarping": false,                     // 是否使用文本图像矫正
              "UseTextlineOrientation": false              // 是否使用文本行方向分类
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ApiUrl": "https://your-id.aistudio-app.com", // PaddleOCR 服务 API 地址
              "Token": ""                                    // 服务 Token；未开启鉴权时留空
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(PaddleOcrConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            return Task.FromResult<string?>("API 地址不能为空");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<PaddleOcrResponse> RunAsync(PaddleOcrRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.File))
        {
            throw new BusinessException(400, "文件 File 不能为空");
        }

        // 每次执行按实例配置设置 BaseAddress：IPaddleocrClient 为 transient，
        // 每次插件执行由 DI 创建一个新实例，HttpClient 同样独立，并发运行互不干扰.
        _client.Client.BaseAddress = new Uri(_config.ApiUrl.Trim());

        var paddleRequest = new InfraPaddleModels.PaddleOcrRequest
        {
            File = request.File,
            FileType = request.FileType,
            UseDocOrientationClassify = request.UseDocOrientationClassify,
            UseDocUnwarping = request.UseDocUnwarping,
            UseTextlineOrientation = request.UseTextlineOrientation,
        };

        InfraPaddle.PaddleOcrResponse<InfraPaddleModels.PaddleOcrResult> response;
        try
        {
            response = await _client.OcrAsync(PaddleOcrAuthorization.Build(_config.Token), paddleRequest, cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException；统一为带响应内容的业务异常，便于在运行抽屉中定位（401/403/429/5xx 等）.
            throw new BusinessException((int)ex.StatusCode, $"PaddleOCR 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        HandleApiError(response);

        var pages = (response.Result?.OcrResults ?? new List<InfraPaddleModels.PaddleOcrResultItem>())
            .Select(MapPage)
            .ToList();

        return new PaddleOcrResponse { Pages = pages };
    }

    /// <summary>
    /// 把单个 OCR 结果项归一为 <see cref="PaddleOcrPage"/>：文本由 <c>rec_texts</c> 按行拼接.
    /// </summary>
    /// <param name="item">PaddleOCR 服务端返回的单页 OCR 结果.</param>
    /// <returns>归一后的页面结果.</returns>
    private static PaddleOcrPage MapPage(InfraPaddleModels.PaddleOcrResultItem item)
    {
        var builder = new StringBuilder();
        if (item.PrunedResult.HasValue
            && item.PrunedResult.Value.ValueKind == JsonValueKind.Object
            && item.PrunedResult.Value.TryGetProperty("rec_texts", out var recTexts)
            && recTexts.ValueKind == JsonValueKind.Array)
        {
            foreach (var text in recTexts.EnumerateArray())
            {
                if (text.ValueKind == JsonValueKind.String)
                {
                    builder.AppendLine(text.GetString());
                }
            }
        }

        return new PaddleOcrPage
        {
            Text = builder.ToString(),
            OcrImage = item.OcrImage,
            InputImage = item.InputImage,
        };
    }

    /// <summary>
    /// 校验 PaddleOCR 业务错误码；非 0 时抛出 <see cref="BusinessException"/>.
    /// </summary>
    /// <typeparam name="T">响应数据类型.</typeparam>
    /// <param name="response">PaddleOCR 响应包装.</param>
    private static void HandleApiError<T>(InfraPaddle.PaddleOcrResponse<T> response)
    {
        if (response.ErrorCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(response.ErrorMsg) ? "未知错误" : response.ErrorMsg;
            throw new BusinessException(500, $"PaddleOCR API 错误（{response.ErrorCode}）：{message}");
        }
    }
}