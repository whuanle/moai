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
/// PaddleOCR 视觉语言模型文档解析（动态插件）：使用实例配置中的 API 地址与 Token 调用 PaddleOCR-VL 接口，由模型直接产出 Markdown.
/// </summary>
/// <remarks>
/// 与旧 <c>NativePlugin.PaddleocrVlPlugin</c> 行为对齐并补齐结构化字段：
/// <list type="bullet">
/// <item><description><see cref="InfraPaddle.IPaddleocrClient"/> 由基础设施层注册，插件内不新建 <c>HttpClient</c>；</description></item>
/// <item><description>API 地址来自实例配置 <see cref="PaddleOcrConfig.ApiUrl"/>，通过 <c>HttpClient.BaseAddress</c> 在每次执行前设置；</description></item>
/// <item><description>鉴权头按 PaddleOCR 协议拼 <c>token {Token}</c>（见 <see cref="PaddleOcrAuthorization"/>），未开启鉴权时为空字符串；</description></item>
/// <item><description>旧版 <c>InvokeAsync</c> 把多页 <c>Markdown.Text</c> 用换行拼接为单字符串，新版本改为按页返回 <see cref="PaddleVlPage"/>，含 <c>MarkdownText</c>、Markdown 内嵌图片（相对路径 → Base64）、输入图与 <c>prunedResult</c> 原文 JSON.</description></item>
/// <item><description>响应 <c>errorCode != 0</c> 归一为 <see cref="BusinessException"/>，由 <c>PluginExecutor</c> 转为运行结果失败.</description></item>
/// </list>
/// </remarks>
[AiPlugin(key: "paddleocr_vl", Name = "PaddleOCR 视觉语言模型文档解析 (VL)", Description = "使用 PaddleOCR-VL 视觉语言模型进行文档解析，直接产出格式完美的 Markdown，适合复杂文档")]
public class PaddleVlPlugin : IDynamicPluginRuntime<PaddleVlRequest, PaddleVlResponse, PaddleOcrConfig>
{
    private readonly InfraPaddle.IPaddleocrClient _client;
    private PaddleOcrConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleVlPlugin"/> class.
    /// </summary>
    /// <param name="client">PaddleOCR API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public PaddleVlPlugin(InfraPaddle.IPaddleocrClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "File": "https://example.com/document.pdf", // 文档文件 URL；亦可直接填 Base64
              "FileType": 0,                                // 0=PDF，1=图像；缺省时由服务端推断
              "UseDocOrientationClassify": false,           // 是否使用文档方向分类
              "UseDocUnwarping": false,                     // 是否使用文本图像矫正
              "UseLayoutDetection": false,                  // 是否使用版面区域检测排序
              "UseChartRecognition": false,                 // 是否使用图表解析
              "PrettifyMarkdown": false,                    // 是否输出美化后的 Markdown
              "ShowFormulaNumber": false                    // Markdown 中是否包含公式编号
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
    public async Task<PaddleVlResponse> RunAsync(PaddleVlRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.File))
        {
            throw new BusinessException(400, "文件 File 不能为空");
        }

        _client.Client.BaseAddress = new Uri(_config.ApiUrl.Trim());

        // 与旧版保持一致：强制开启 Visualize 以便 outputImages/inputImage 字段可被服务端产出，
        // 其它用户传入的开关按需透传.
        var paddleRequest = new InfraPaddleModels.PaddleOcrVlRequest
        {
            File = request.File,
            FileType = request.FileType,
            UseDocOrientationClassify = request.UseDocOrientationClassify,
            UseDocUnwarping = request.UseDocUnwarping,
            UseLayoutDetection = request.UseLayoutDetection,
            UseChartRecognition = request.UseChartRecognition,
            PrettifyMarkdown = request.PrettifyMarkdown,
            ShowFormulaNumber = request.ShowFormulaNumber,
            Visualize = true,
        };

        InfraPaddle.PaddleOcrResponse<InfraPaddleModels.PaddleLayoutParsingResult> response;
        try
        {
            response = await _client.PaddleOCRVLAsync(PaddleOcrAuthorization.Build(_config.Token), paddleRequest, cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException；统一为带响应内容的业务异常，便于在运行抽屉中定位（401/403/429/5xx 等）.
            throw new BusinessException((int)ex.StatusCode, $"PaddleOCR 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        HandleApiError(response);

        var pages = (response.Result?.LayoutParsingResults ?? new List<InfraPaddleModels.PaddleLayoutParsingResultItem>())
            .Select(MapPage)
            .ToList();

        return new PaddleVlResponse { Pages = pages };
    }

    /// <summary>
    /// 把单个版面解析结果归一为 <see cref="PaddleVlPage"/>：Markdown 文本、Markdown 内嵌图片、输入图与 <c>prunedResult</c> 原文 JSON 分别抽取.
    /// </summary>
    /// <param name="item">PaddleOCR 服务端返回的单页版面解析结果.</param>
    /// <returns>归一后的页面结果.</returns>
    private static PaddleVlPage MapPage(InfraPaddleModels.PaddleLayoutParsingResultItem item)
    {
        return new PaddleVlPage
        {
            MarkdownText = item.Markdown?.Text,
            MarkdownImages = item.Markdown?.Images,
            InputImage = item.InputImage,
            PrunedResultJson = SerializePrunedResult(item.PrunedResult),
        };
    }

    /// <summary>
    /// 把 <c>prunedResult</c>（<see cref="JsonElement"/>）序列化为可读 JSON 文本.
    /// </summary>
    /// <param name="prunedResult">来自 PaddleOCR 服务端的 <c>prunedResult</c> 字段；可能为 <see langword="null"/>.</param>
    /// <returns>序列化后的 JSON 文本；空值或非对象/数组时返回 <see langword="null"/>.</returns>
    private static string? SerializePrunedResult(JsonElement? prunedResult)
    {
        if (!prunedResult.HasValue)
        {
            return null;
        }

        var value = prunedResult.Value;
        if (value.ValueKind != JsonValueKind.Object && value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value.GetRawText();
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