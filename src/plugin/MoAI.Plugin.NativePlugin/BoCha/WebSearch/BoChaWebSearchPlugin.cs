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

namespace MoAI.Plugin.Plugins.BoCha.WebSearch;

/// <summary>
/// BoCha 全网搜索插件
/// </summary>
[NativePluginConfig(
    "bocha_web_search",
    Name = "BoCha 全网搜索",
    Description = "使用 BoCha API 进行全网搜索",
    Classify = NativePluginClassify.Search,
    ConfigType = typeof(BoChaPluginConfig))]
[InjectOnTransient]
[Description("使用 BoCha API 进行全网搜索")]

public partial class BoChaWebSearchPlugin : INativePluginRuntime
{
    private readonly IBoChaClient _boChaClient;
    private BoChaPluginConfig _config = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoChaWebSearchPlugin"/> class.
    /// </summary>
    /// <param name="boChaClient">BoCha 客户端。</param>
    public BoChaWebSearchPlugin(IBoChaClient boChaClient)
    {
        _boChaClient = boChaClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new WebSearchRequest
        {
            Query = "如何学习 C#",
            Freshness = "oneMonth",
            Count = 5,
            Summary = false,
            Exclude = null!,
            Include = null!
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
            var testParams = JsonSerializer.Deserialize<WebSearchRequest>(@params)!;
            var result = await InvokeAsync(testParams.Query, testParams.Summary, testParams.Freshness, testParams.Count, testParams.Include, testParams.Exclude);
            return JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <summary>
    /// 执行 BoCha 全网搜索。
    /// </summary>
    /// <param name="query">用户的搜索词。</param>
    /// <param name="summary">是否显示文本摘要。</param>
    /// <param name="freshness">搜索指定时间范围内的网页。</param>
    /// <param name="count">返回结果的条数。</param>
    /// <param name="include">指定搜索的网站范围。</param>
    /// <param name="exclude">排除搜索的网站范围。</param>
    /// <returns>搜索结果。</returns>
    [KernelFunction("invoke")]
    [Description("执行 BoCha 全网搜索")]
    public async Task<WebSearchData> InvokeAsync(
        [Description("用户的搜索词")] string query,
        [Description("是否显示文本摘要 (true/false)")] bool? summary = null,
        [Description("搜索时间范围 (e.g., noLimit, oneDay, oneWeek, oneMonth, oneYear, YYYY-MM-DD..YYYY-MM-DD)")] string freshness = "noLimit",
        [Description("返回结果的条数 (1-50)")] int count = 10,
        [Description("指定搜索的网站范围 (e.g., qq.com|m.163.com)")] string? include = null,
        [Description("排除搜索的网站范围 (e.g., qq.com|m.163.com)")] string? exclude = null)
    {
        if (string.IsNullOrWhiteSpace(_config?.Key))
        {
            throw new BusinessException("API Key 未配置。");
        }

        var request = new WebSearchRequest
        {
            Query = query,
            Summary = summary,
            Freshness = freshness,
            Count = count,
            Include = include,
            Exclude = exclude
        };

        var response = await _boChaClient.WebSearchAsync($"Bearer {_config.Key}", request);
        _boChaClient.HandleApiError(response);
        return response.Data;
    }
}