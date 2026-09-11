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
/// PaddleOCR 文档版面解析（动态插件）：使用实例配置中的 API 地址与 Token 调用 PaddleOCR PP-StructureV3 接口分析带表格、公式、印章、图表的复杂文档.
/// </summary>
/// <remarks>
/// 与旧 <c>NativePlugin.PaddleocrStructureV3Plugin</c> 行为对齐：
/// <list type="bullet">
/// <item><description>旧版 <c>InvokeAsync</c> 只回填了印章识别的 <c>rec_texts</c>，结构信息被丢弃；新版本按页返回 <c>prunedResult</c> 原文 JSON、印章文本、输入图与输出图，便于上层按需解析.</description></item>
/// <item><description><see cref="InfraPaddle.IPaddleocrClient"/> 由基础设施层注册，插件内不新建 <c>HttpClient</c>；</description></item>
/// <item><description>API 地址来自实例配置 <see cref="PaddleOcrConfig.ApiUrl"/>，通过 <c>HttpClient.BaseAddress</c> 在每次执行前设置；</description></item>
/// <item><description>鉴权头按 PaddleOCR 协议拼 <c>token {Token}</c>（见 <see cref="PaddleOcrAuthorization"/>），未开启鉴权时为空字符串；</description></item>
/// <item><description>响应 <c>errorCode != 0</c> 归一为 <see cref="BusinessException"/>，由 <c>PluginExecutor</c> 转为运行结果失败.</description></item>
/// </list>
/// </remarks>
[AiPlugin(key: "paddleocr_structure_v3", Name = "PaddleOCR 文档解析 (StructureV3)", Description = "使用 PaddleOCR PP-StructureV3 模型对复杂文档进行版面分析，支持表格/公式/印章/图表识别，速度较慢，建议单页处理")]
public class PaddleStructureV3Plugin : IDynamicPluginRuntime<PaddleStructureV3Request, PaddleStructureV3Response, PaddleOcrConfig>
{
    private readonly InfraPaddle.IPaddleocrClient _client;
    private PaddleOcrConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PaddleStructureV3Plugin"/> class.
    /// </summary>
    /// <param name="client">PaddleOCR API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public PaddleStructureV3Plugin(InfraPaddle.IPaddleocrClient client)
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
              "UseTableRecognition": false,                 // 是否使用表格识别子产线
              "UseFormulaRecognition": false,               // 是否使用公式识别子产线
              "UseSealRecognition": false,                  // 是否使用印章识别子产线
              "UseChartRecognition": false,                 // 是否使用图表解析
              "UseRegionDetection": false                   // 是否使用文档区域检测
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
    public async Task<PaddleStructureV3Response> RunAsync(PaddleStructureV3Request request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.File))
        {
            throw new BusinessException(400, "文件 File 不能为空");
        }

        _client.Client.BaseAddress = new Uri(_config.ApiUrl.Trim());

        var paddleRequest = new InfraPaddleModels.PaddleLayoutParsingRequest
        {
            File = request.File,
            FileType = request.FileType,
            UseDocOrientationClassify = request.UseDocOrientationClassify,
            UseDocUnwarping = request.UseDocUnwarping,
            UseTableRecognition = request.UseTableRecognition,
            UseFormulaRecognition = request.UseFormulaRecognition,
            UseSealRecognition = request.UseSealRecognition,
            UseChartRecognition = request.UseChartRecognition,
            UseRegionDetection = request.UseRegionDetection,
        };

        InfraPaddle.PaddleOcrResponse<InfraPaddleModels.PaddleLayoutParsingResult> response;
        try
        {
            response = await _client.StructureV3Async(PaddleOcrAuthorization.Build(_config.Token), paddleRequest, cancellationToken)
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

        return new PaddleStructureV3Response { Pages = pages };
    }

    /// <summary>
    /// 把单个版面解析结果归一为 <see cref="PaddleStructureV3Page"/>：原文 JSON、印章文本与输入/输出图像分别抽取.
    /// </summary>
    /// <param name="item">PaddleOCR 服务端返回的单页版面解析结果.</param>
    /// <returns>归一后的页面结果.</returns>
    private static PaddleStructureV3Page MapPage(InfraPaddleModels.PaddleLayoutParsingResultItem item)
    {
        return new PaddleStructureV3Page
        {
            PrunedResultJson = SerializePrunedResult(item.PrunedResult),
            OutputImages = item.OutputImages,
            InputImage = item.InputImage,
            SealTexts = ExtractSealTexts(item.PrunedResult),
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
    /// 从 <c>prunedResult.seal_res_list</c> 抽出每条印章的 <c>rec_texts</c>（取首条作为该印章的识别结果）.
    /// </summary>
    /// <param name="prunedResult">来自 PaddleOCR 服务端的 <c>prunedResult</c> 字段.</param>
    /// <returns>印章识别文本列表；字段缺失或形态不符时为空列表.</returns>
    /// <remarks>
    /// 旧 <c>NativePlugin.PaddleocrStructureV3Plugin.InvokeAsync</c> 仅取 <c>rec_texts</c> 的首条并拼接为单字符串；
    /// 新版本改为按印章拆为列表，方便上层逐条使用.
    /// </remarks>
    private static List<string> ExtractSealTexts(JsonElement? prunedResult)
    {
        if (!prunedResult.HasValue || prunedResult.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!prunedResult.Value.TryGetProperty("seal_res_list", out var sealList) || sealList.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var seal in sealList.EnumerateArray())
        {
            if (seal.ValueKind != JsonValueKind.Object
                || !seal.TryGetProperty("rec_texts", out var recTexts)
                || recTexts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            // 旧实现取 rec_texts[0]；沿用相同语义，兼容历史行为；调用方可在外部按需扩展为完整数组.
            var first = recTexts.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.String)
            {
                result.Add(first.GetString() ?? string.Empty);
            }
        }

        return result;
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