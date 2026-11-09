using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphText2CypherCommand"/>
/// 链路：schema 自描述 → 对话模型生成 Cypher（D30 三级回退：请求级 &gt; 图谱默认抽取模型 &gt; 400）→ KG 本地只读守卫 →
/// IKgCypherAccessService 执行（$kgId 注入 + 结果侧归属校验）→ 表格结果 + 节点 id 抽取（画布高亮）；结果 500 行封顶.
/// </summary>
public class QueryKnowledgeGraphText2CypherCommandHandler : IRequestHandler<QueryKnowledgeGraphText2CypherCommand, QueryKnowledgeGraphText2CypherCommandResponse>
{
    /// <summary>结果行数上限（与画布节点封顶同量级）.</summary>
    public const int MaxRows = 500;

    /// <summary>生成 Cypher 的输出 token 预算.</summary>
    private const int MaxOutputTokens = 1024;

    private const int QueryTimeoutSeconds = 30;

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IKgCypherAccessService _cypherAccessService;
    private readonly IAiChatCompletionService _chatCompletionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphText2CypherCommandHandler"/> class.
    /// </summary>
    public QueryKnowledgeGraphText2CypherCommandHandler(
        DatabaseContext databaseContext,
        IKnowledgeGraphAuthorizer authorizer,
        IKnowledgeGraphStore store,
        IKgCypherAccessService cypherAccessService,
        IAiChatCompletionService chatCompletionService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
        _cypherAccessService = cypherAccessService;
        _chatCompletionService = chatCompletionService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphText2CypherCommandResponse> Handle(QueryKnowledgeGraphText2CypherCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            // 接入图问图走 kg_cypher_query 插件（按库路由），画布端点仅托管图
            throw new BusinessException("接入图谱不支持画布问图，可使用 Text2Cypher 插件查询.") { StatusCode = 409 };
        }

        // D30 三级回退
        var modelId = request.AiModelId ?? graph.ExtractModelId
            ?? throw new BusinessException("未指定模型：请选择 AI 对话模型或在图谱设置中配置默认抽取模型.") { StatusCode = 400 };

        var (model, channel) = await ResolveConversationModelAsync(modelId, graph.TeamId, cancellationToken);

        // 1. 结构自描述 + 提示词
        var digest = await _cypherAccessService.GetSchemaDigestAsync(request.KnowledgeGraphId, cancellationToken);
        var prompt = BuildPrompt(digest, request.Question);

        // 2. 生成 Cypher
        var output = await _chatCompletionService.CompleteTextAsync(
            model,
            channel,
            prompt,
            new AiChatCompletionOptions
            {
                DisableThinking = true,
                MaxOutputTokens = MaxOutputTokens,
                Temperature = 0.1f,
            },
            cancellationToken);
        var cypher = ExtractCypher(output);
        if (string.IsNullOrWhiteSpace(cypher))
        {
            throw new BusinessException("模型未能生成有效的 Cypher 查询，请换一种问法或更换模型.") { StatusCode = 400 };
        }

        // 3. 只读守卫（教学式错误直接抛 400）
        var guardError = KgCypherReadOnlyGuard.Validate(cypher);
        if (guardError != null)
        {
            throw new BusinessException($"生成的查询未通过只读校验：{guardError}") { StatusCode = 400 };
        }

        // 4. 执行（$kgId 注入与结果侧归属校验由访问服务负责）
        KgCypherQueryResult result;
        try
        {
            result = await _cypherAccessService.ExecuteQueryAsync(
                request.KnowledgeGraphId,
                cypher,
                parameters: null,
                maxRows: MaxRows,
                timeoutSeconds: QueryTimeoutSeconds,
                cancellationToken);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"查询执行失败：{TrimError(ex.Message)}") { StatusCode = 400 };
        }

        // 5. 结果行抽取本图节点 id（string 形态 Guid 命中即收集）
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in result.Rows)
        {
            foreach (var value in row.Values)
            {
                if (value is string text && Guid.TryParse(text, out _))
                {
                    candidateIds.Add(text);
                }
            }
        }

        var nodeIds = new List<string>();
        if (candidateIds.Count > 0)
        {
            var existing = await _store.GetNodeTypesByIdsAsync(request.KnowledgeGraphId, candidateIds.ToList(), cancellationToken);
            nodeIds = existing.Keys.ToList();
        }

        return new QueryKnowledgeGraphText2CypherCommandResponse
        {
            Ok = true,
            Cypher = cypher,
            Columns = result.Columns.ToList(),
            Rows = result.Rows.Select(row => new Dictionary<string, object?>(row)).ToList(),
            RowCount = result.RowCount,
            Truncated = result.Truncated,
            NodeIds = nodeIds,
        };
    }

    private static string TrimError(string message)
        => message.Length <= 300 ? message : message[..300];

    private static string ExtractCypher(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var text = output.Trim();
        // 剥代码块围栏
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                text = text[(firstLineEnd + 1)..].Trim();
            }

            var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                text = text[..fenceEnd].Trim();
            }
        }

        // 多语句取第一条非空行段
        var lines = text.Split('\n');
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            var current = line.TrimEnd();
            if (current.Length == 0 && builder.Length == 0)
            {
                continue;
            }

            builder.AppendLine(current);
            if (current.EndsWith(';'))
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }

    private static string BuildPrompt(KgCypherSchemaDigest digest, string question)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是知识图谱 Cypher 查询生成器。根据下述图谱结构与用户问题，只输出一条只读 Cypher 查询，不要输出任何解释或代码块围栏。");
        builder.AppendLine("图谱结构（JSON）：");
        builder.AppendLine(JsonSerializer.Serialize(digest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        builder.AppendLine("要求：");
        builder.AppendLine("1. 查询必须以 MATCH 等只读关键字开头，禁止任何写操作；");
        builder.AppendLine("2. 托管图谱查询必须包含 {kgId: $kgId} 节点过滤（参数由系统自动注入，不要自行赋值），例如 MATCH (n:KgNode {kgId: $kgId})；");
        builder.AppendLine("3. 实体类型（entityTypeId 对应的类型名）与关系类型必须从结构清单中选择；");
        builder.AppendLine("4. 最多返回 100 行（用 LIMIT 限制），需要名称/描述等信息用属性投影返回。");
        builder.AppendLine($"用户问题：{question}");
        return builder.ToString();
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveConversationModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var usableModelIds = await _databaseContext.GetTeamUsableModelIdsAsync(teamId, cancellationToken);
        var model = await (from m in _databaseContext.AiModels
                           join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                           where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                               && usableModelIds.Contains(m.Id)
                           select new { m, c })
                           .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException("AI 对话模型不存在、未启用或团队不可用.") { StatusCode = 400 };

        if (!string.Equals(model.m.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是对话模型.") { StatusCode = 400 };
        }

        return (model.m, model.c);
    }
}
