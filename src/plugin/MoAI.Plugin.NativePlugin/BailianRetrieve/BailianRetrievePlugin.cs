#pragma warning disable CA1031 // 不捕获常规异常类型

using AlibabaCloud.SDK.Bailian20231229;
using AlibabaCloud.SDK.Bailian20231229.Models;
using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins;
using System.ComponentModel;
using System.Text.Json;

namespace MoAI.Plugin.Plugins.BailianRetrieve;

/// <summary>
/// 阿里云百炼知识库检索插件.
/// </summary>
[NativePluginConfig(
    "bailian_retrieve",
    Name = "阿里云百炼知识库检索",
    Description = "使用阿里云百炼 API 进行知识库检索，支持向量检索和关键词检索",
    Classify = NativePluginClassify.Search,
    ConfigType = typeof(BailianRetrievePluginConfig))]
[InjectOnTransient]
[Description("使用阿里云百炼 API 进行知识库检索")]
public class BailianRetrievePlugin : INativePluginRuntime
{
    private BailianRetrievePluginConfig _config = default!;
    private Client? _client;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new BailianRetrieveParams
        {
            Query = "数据库安全链接字符串"
        };

        return JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <inheritdoc/>
    public Task<string?> CheckConfigAsync(string config)
    {
        try
        {
            var objectParams = JsonSerializer.Deserialize<BailianRetrievePluginConfig>(config);
            if (string.IsNullOrWhiteSpace(objectParams?.AccessKeyId))
            {
                return Task.FromResult<string?>("AccessKeyId 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(objectParams?.AccessKeySecret))
            {
                return Task.FromResult<string?>("AccessKeySecret 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(objectParams?.WorkspaceId))
            {
                return Task.FromResult<string?>("WorkspaceId 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(objectParams?.IndexId))
            {
                return Task.FromResult<string?>("IndexId 不能为空。");
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
        _config = JsonSerializer.Deserialize<BailianRetrievePluginConfig>(config) ?? new BailianRetrievePluginConfig();

        // 创建阿里云百炼客户端
        var sdkConfig = new AlibabaCloud.OpenApiClient.Models.Config
        {
            AccessKeyId = _config.AccessKeyId,
            AccessKeySecret = _config.AccessKeySecret,
            Endpoint = "bailian.cn-beijing.aliyuncs.com"
        };
        _client = new Client(sdkConfig);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var testParams = JsonSerializer.Deserialize<BailianRetrieveParams>(@params)!;
            var result = await InvokeAsync(testParams.Query);
            return JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    /// <summary>
    /// 执行阿里云百炼知识库检索.
    /// </summary>
    /// <param name="query">查询提示词.</param>
    /// <returns>检索结果.</returns>
    [KernelFunction("invoke")]
    [Description("执行阿里云百炼知识库检索")]
    public async Task<List<BailianRetrieveResultNode>> InvokeAsync(
        [Description("查询提示词")] string query)
    {
        if (_client == null)
        {
            throw new BusinessException("百炼客户端未初始化，请先配置插件。");
        }

        var request = new RetrieveRequest
        {
            Query = query,
            IndexId = _config.IndexId,
        };

        if (_config.DenseSimilarityTopK.HasValue)
        {
            request.DenseSimilarityTopK = _config.DenseSimilarityTopK.Value;
        }

        if (_config.SparseSimilarityTopK.HasValue)
        {
            request.SparseSimilarityTopK = _config.SparseSimilarityTopK.Value;
        }

        var response = await _client.RetrieveAsync(_config.WorkspaceId, request);

        if (response.Body == null || !response.Body.Success.GetValueOrDefault())
        {
            throw new BusinessException($"检索失败: {response.Body?.Message ?? "未知错误"}");
        }

        var nodes = response.Body.Data?.Nodes ?? [];
        return nodes.Select(n => new BailianRetrieveResultNode
        {
            Text = n.Text ?? string.Empty,
            Score = n.Score ?? 0
        }).ToList();
    }
}

/// <summary>
/// 检索结果节点.
/// </summary>
public class BailianRetrieveResultNode
{
    /// <summary>
    /// 文本内容.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 相似度分数.
    /// </summary>
    public double Score { get; set; }
}
