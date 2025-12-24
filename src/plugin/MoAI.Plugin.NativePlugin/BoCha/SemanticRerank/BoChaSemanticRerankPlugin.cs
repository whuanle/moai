#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.BoCha;
using MoAI.Infra.BoCha.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins.BoCha.Common;
using System.ComponentModel;
using System.Text.Json;

namespace MoAI.Plugin.Plugins.BoCha.SemanticRerank;

/// <summary>
/// BoCha 语义排序插件
/// </summary>
[NativePluginConfig(
    "bocha_semantic_rerank",
    Name = "BoCha 语义排序",
    Description = "使用 BoCha API 对文档进行语义排序，返回 query 跟 documents 每个文本的相关度",
    Classify = NativePluginClassify.Search,
    ConfigType = typeof(BoChaPluginConfig))]
[InjectOnTransient]
[Description("使用 BoCha API 对文档进行语义排序，返回 query 跟 documents 每个文本的相关度")]
public partial class BoChaSemanticRerankPlugin : INativePluginRuntime
{
    private readonly IBoChaClient _boChaClient;
    private BoChaPluginConfig _config = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoChaSemanticRerankPlugin"/> class.
    /// </summary>
    /// <param name="boChaClient">BoCha 客户端。</param>
    public BoChaSemanticRerankPlugin(IBoChaClient boChaClient)
    {
        _boChaClient = boChaClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new SemanticRerankRequest
        {
            Query = "全球变暖",
            Documents = new List<string>
            {
                "全球变暖导致海平面上升。",
                "苹果公司发布了新款手机。",
                "气候变化对农业产生了深远影响。"
            },
            Model = "gte-rerank",
            ReturnDocuments = false,
            TopN = 3
        };

        return JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <inheritdoc/>
    public Task<string?> CheckConfigAsync(string config)
    {
        try
        {
            var objectParams = JsonSerializer.Deserialize<BoChaPluginConfig>(config);
            if (string.IsNullOrWhiteSpace(objectParams?.Key))
            {
                return Task.FromResult<string?>("API Key 不能为空。");
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
        _config = JsonSerializer.Deserialize<BoChaPluginConfig>(config) ?? new BoChaPluginConfig();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var testParams = JsonSerializer.Deserialize<SemanticRerankRequest>(@params)!;

            var result = await InvokeAsync(testParams.Query, testParams.Documents, testParams.Model, testParams.TopN, testParams.ReturnDocuments);
            return JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <summary>
    /// 执行 BoCha 语义排序。
    /// </summary>
    /// <param name="query">用户的搜索词。</param>
    /// <param name="documents">需要排序的文档数组。</param>
    /// <param name="model">排序使用的模型版本。</param>
    /// <param name="topN">排序返回的Top文档数量。</param>
    /// <param name="returnDocuments">是否返回每一条document原文。</param>
    /// <returns>排序结果。</returns>
    [KernelFunction("invoke")]
    [Description("执行 BoCha 语义排序，返回 query 跟 documents 每个文本的相关度")]
    public async Task<SemanticRerankData> InvokeAsync(
        [Description("用户的搜索词")] string query,
        [Description("需要排序的文档数组")] List<string> documents,
        [Description("排序模型 (e.g., bocha-semantic-reranker-cn, bocha-semantic-reranker-en)")] string model = "bocha-semantic-reranker-cn",
        [Description("返回的Top文档数量")] int? topN = null,
        [Description("是否返回文档原文")] bool? returnDocuments = null)
    {
        if (string.IsNullOrWhiteSpace(_config?.Key))
        {
            throw new BusinessException("API Key 未配置。");
        }

        var request = new SemanticRerankRequest
        {
            Query = query,
            Documents = documents,
            Model = model,
            TopN = topN,
            ReturnDocuments = returnDocuments
        };

        var response = await _boChaClient.SemanticRerankAsync($"Bearer {_config.Key}", request);
        _boChaClient.HandleApiError(response);

        return response.Data;
    }
}