using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 知识图谱只读查询（动态插件 Text2Cypher）：绑定一张团队图谱，对话模型直接写只读 Cypher 查询实体与关系.
/// </summary>
/// <remarks>
/// 只读与隔离分三层（见 <see cref="CypherReadOnlyGuard"/> 与 IKgCypherAccessService 实现）：
/// <list type="number">
/// <item><description>**文本层**：黑名单关键字/多语句/超长在 <see cref="CypherReadOnlyGuard"/> 拒绝.</description></item>
/// <item><description>**隔离层**：托管图强制 $kgId 参数并由服务端注入真实图谱 id（结果侧另做 kgId 归属校验）；接入图按库路由天然隔离.</description></item>
/// <item><description>**资源层**：事务超时 + 行数截断在访问服务内兜底.</description></item>
/// </list>
/// Cypher 由对话模型生成（设计文档方案 A），纠错回路靠教学式错误信息回喂对话循环，无内置重试.
/// </remarks>
[AiPlugin(
    key: "kg_cypher_query",
    Name = "知识图谱只读查询",
    Description = "绑定一张知识图谱，用只读 Cypher 查询其中的实体与关系。先传 {\"Schema\": true} 获取图谱结构与示例节点，再写 MATCH 查询；托管图谱查询必须包含 {kgId: $kgId} 过滤（参数自动注入）")]
public class KgCypherQueryPlugin : IDynamicPluginRuntime<KgCypherQueryRequest, KgCypherQueryResponse, KgCypherQueryConfig>
{
    /// <summary>返回行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>返回行数上限的上界.</summary>
    private const int MaxMaxRows = 1000;

    /// <summary>查询超时的下界（秒）.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>查询超时的上界（秒）.</summary>
    private const int MaxTimeoutSeconds = 300;

    private readonly IKgCypherAccessService _accessService;
    private KgCypherQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KgCypherQueryPlugin"/> class.
    /// </summary>
    /// <param name="accessService">知识图谱只读访问服务.</param>
    public KgCypherQueryPlugin(IKgCypherAccessService accessService)
    {
        _accessService = accessService;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Schema": true
              // 查询模式示例：
              // "Cypher": "MATCH (n:KgNode {kgId: $kgId}) WHERE n.name CONTAINS $kw RETURN n.name LIMIT 20",
              // "Params": { "kw": "仓库" }
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "KgId": 123,          // 绑定的知识图谱 id
              "MaxRows": 200,       // 单次最多返回行数，1-1000
              "TimeoutSeconds": 30  // 查询超时秒数，1-300
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(KgCypherQueryConfig config)
    {
        if (config.KgId <= 0)
        {
            return Task.FromResult<string?>("配置 KgId 必须大于 0（绑定要查询的知识图谱 id）");
        }

        _config = new KgCypherQueryConfig
        {
            KgId = config.KgId,
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<KgCypherQueryResponse> RunAsync(KgCypherQueryRequest request, CancellationToken cancellationToken)
    {
        if (request.Schema)
        {
            var digest = await _accessService.GetSchemaDigestAsync(_config.KgId, cancellationToken).ConfigureAwait(false);
            return new KgCypherQueryResponse
            {
                GraphType = digest.GraphType,
                Dialect = digest.Dialect,
                EntityTypes = digest.EntityTypes,
                RelationTypes = digest.RelationTypes,
                SampleNodes = digest.SampleNodes,
                Usage = digest.Usage,
            };
        }

        var violation = CypherReadOnlyGuard.Validate(request.Cypher);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        var result = await _accessService.ExecuteQueryAsync(
            _config.KgId,
            request.Cypher,
            NormalizeParams(request.Params),
            _config.MaxRows,
            _config.TimeoutSeconds,
            cancellationToken).ConfigureAwait(false);

        return new KgCypherQueryResponse
        {
            Columns = result.Columns,
            Rows = result.Rows,
            RowCount = result.RowCount,
            Truncated = result.Truncated,
        };
    }

    /// <summary>
    /// 把 Params 里的 JsonElement 归一为基础 CLR 类型，供图库驱动使用.
    /// </summary>
    /// <param name="parameters">原始参数.</param>
    /// <returns>归一后的参数；空返回 null.</returns>
    private static Dictionary<string, object?>? NormalizeParams(Dictionary<string, object?>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(parameters.Count, StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            result[pair.Key] = pair.Value switch
            {
                null => null,
                JsonElement element => ConvertElement(element),
                _ => pair.Value,
            };
        }

        return result;
    }

    /// <summary>
    /// JsonElement 转基础 CLR 类型.
    /// </summary>
    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };
}
