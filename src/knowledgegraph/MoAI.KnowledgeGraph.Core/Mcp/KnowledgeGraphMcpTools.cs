using System.ComponentModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MoAI.KnowledgeGraph.Mcp;

/// <summary>
/// 知识图谱 MCP 工具集：以团队/应用接入 key（ExternalAuthenticationMiddleware 已完成 kg_mcp 鉴权与 kgId 归属校验）只读访问团队托管图谱，
/// 暴露 图谱列表 / schema / 节点搜索 / 向量召回 四个只读工具。SDK 对每次工具调用构造一个实例（请求 DI 作用域内），可安全注入 scoped 服务.
/// </summary>
[McpServerToolType]
public class KnowledgeGraphMcpTools
{
    private const int MaxPageSize = 50;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMediator _mediator;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly ILogger<KnowledgeGraphMcpTools> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphMcpTools"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP 上下文访问器（读取鉴权上下文与路由 kgId）.</param>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="externalAuthorizer">外部知识图谱授权器.</param>
    /// <param name="logger">日志.</param>
    public KnowledgeGraphMcpTools(IHttpContextAccessor httpContextAccessor, IMediator mediator, IExternalKnowledgeGraphAuthorizer externalAuthorizer, ILogger<KnowledgeGraphMcpTools> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _mediator = mediator;
        _externalAuthorizer = externalAuthorizer;
        _logger = logger;
    }

    /// <summary>
    /// 获取接入凭证所属团队下的托管图谱列表.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回图谱列表.</returns>
    [McpServerTool(Name = "list_knowledge_graphs", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("获取当前团队有权限访问的知识图谱列表（仅托管图谱），包含图谱 id、名称与描述。后续查询 schema/节点/召回时需要图谱 id。")]
    public async Task<KnowledgeGraphMcpGraphList> ListKnowledgeGraphsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var caller = RequireCaller();
            var response = await _mediator.Send(new QueryExternalGraphsCommand { Caller = caller }, cancellationToken);
            return new KnowledgeGraphMcpGraphList
            {
                TeamId = caller.TeamId,
                Items = response.Items.Select(x => new KnowledgeGraphMcpGraph
                {
                    KgId = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                }).ToList(),
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 获取图谱 schema（实体类型 + 关系类型）.
    /// </summary>
    /// <param name="kgId">图谱 id；不传时使用接入地址中的图谱 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回图谱 schema.</returns>
    [McpServerTool(Name = "get_knowledge_graph_schema", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("获取指定知识图谱的模型定义：实体类型（含属性定义）与关系类型（含起止类型约束）。构造节点搜索或理解召回结果前建议先调用本工具了解图谱结构。可先用 list_knowledge_graphs 获取图谱 id。")]
    public async Task<KnowledgeGraphMcpSchema> GetKnowledgeGraphSchemaAsync(
        [Description("图谱 id；不传时使用接入地址中的图谱 id")] long? kgId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var graph = await RequireGraphAsync(kgId, cancellationToken);
            var caller = RequireCaller();
            var schema = await _mediator.Send(new QueryExternalGraphSchemaCommand { Caller = caller, KnowledgeGraphId = graph.Id }, cancellationToken);
            return new KnowledgeGraphMcpSchema
            {
                KgId = graph.Id,
                KgName = graph.Name,
                EntityTypes = schema.EntityTypes.Select(x => new KnowledgeGraphMcpEntityType
                {
                    EntityTypeId = x.EntityTypeId ?? 0,
                    Name = x.Name,
                    Color = string.IsNullOrEmpty(x.Color) ? null : x.Color,
                    Description = string.IsNullOrEmpty(x.Description) ? null : x.Description,
                    Properties = (x.Properties ?? []).Select(p => new KnowledgeGraphMcpPropertyDef
                    {
                        Name = p.Name,
                        Type = p.Type,
                        Required = p.Required,
                        Description = p.Description,
                    }).ToList(),
                }).ToList(),
                RelationTypes = schema.RelationTypes.Select(x => new KnowledgeGraphMcpRelationType
                {
                    RelationTypeId = x.RelationTypeId ?? 0,
                    Name = x.Name,
                    SourceTypeId = x.SourceTypeId,
                    TargetTypeId = x.TargetTypeId,
                    Description = string.IsNullOrEmpty(x.Description) ? null : x.Description,
                }).ToList(),
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 按关键字分页搜索图谱节点.
    /// </summary>
    /// <param name="query">名称关键字，空串返回全部节点.</param>
    /// <param name="kgId">图谱 id；不传时使用接入地址中的图谱 id.</param>
    /// <param name="entityTypeId">按实体类型过滤.</param>
    /// <param name="pageNo">页码，从 1 开始，默认 1.</param>
    /// <param name="pageSize">每页数量，1-50，默认 20.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回节点搜索结果.</returns>
    [McpServerTool(Name = "search_knowledge_graph_nodes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("按名称关键字搜索指定知识图谱内的实体节点（分页），返回节点 id、名称、描述与实体类型。适合精确查找某个已知实体；语义模糊的查找建议用 search_knowledge_graph_recall 向量召回。可先用 get_knowledge_graph_schema 查看实体类型。")]
    // 注意：SDK 把无默认值的参数一律按必填校验，可选参数必须带 = null 默认值
    public async Task<KnowledgeGraphMcpNodeSearchResult> SearchKnowledgeGraphNodesAsync(
        [Description("名称关键字，不传或空串表示返回全部节点")] string? query = null,
        [Description("图谱 id；不传时使用接入地址中的图谱 id")] long? kgId = null,
        [Description("按实体类型 id 过滤（可先用 get_knowledge_graph_schema 查询）")] long? entityTypeId = null,
        [Description("页码，从 1 开始")] int? pageNo = null,
        [Description("每页数量，1-50")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var graph = await RequireGraphAsync(kgId, cancellationToken);
            var caller = RequireCaller();

            var response = await _mediator.Send(new QueryExternalNodesCommand
            {
                Caller = caller,
                KnowledgeGraphId = graph.Id,
                EntityTypeId = entityTypeId,
                Keyword = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
                PageNo = Math.Max(pageNo ?? 1, 1),
                PageSize = Math.Clamp(pageSize ?? 20, 1, MaxPageSize),
            }, cancellationToken);

            var typeNames = (await _mediator.Send(new QueryExternalGraphSchemaCommand { Caller = caller, KnowledgeGraphId = graph.Id }, cancellationToken))
                .EntityTypes.ToDictionary(x => x.EntityTypeId ?? 0, x => x.Name);

            return new KnowledgeGraphMcpNodeSearchResult
            {
                KgId = graph.Id,
                Keyword = query,
                Total = response.Total,
                PageNo = Math.Max(pageNo ?? 1, 1),
                PageSize = Math.Clamp(pageSize ?? 20, 1, MaxPageSize),
                Items = response.Items.Select(x => new KnowledgeGraphMcpNode
                {
                    NodeId = x.NodeId,
                    Name = x.Name,
                    Description = x.Description,
                    EntityTypeId = x.EntityTypeId,
                    EntityTypeName = typeNames.GetValueOrDefault(x.EntityTypeId),
                }).ToList(),
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 在图谱内做向量召回，返回与查询最相关的实体及其一跳邻居.
    /// </summary>
    /// <param name="queryText">查询文本.</param>
    /// <param name="kgId">图谱 id；不传时使用接入地址中的图谱 id.</param>
    /// <param name="top">返回条数，1-50，默认 5.</param>
    /// <param name="minScore">相似度阈值（0-1），低于阈值的命中项被丢弃，不传表示不过滤.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回召回结果.</returns>
    [McpServerTool(Name = "search_knowledge_graph_recall", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("在指定知识图谱内按语义向量召回与查询最相关的实体（名称/描述/类型 + 相似度得分 + 一跳邻居关系），适合回答问题前检索团队知识图谱。需要图谱已配置向量化模型；未配置时返回 skippedHint 说明，可改用 search_knowledge_graph_nodes。可先用 list_knowledge_graphs 获取图谱 id。")]
    public async Task<KnowledgeGraphMcpRecallResult> SearchKnowledgeGraphRecallAsync(
        [Description("查询文本，例如要了解的问题")] string queryText,
        [Description("图谱 id；不传时使用接入地址中的图谱 id")] long? kgId = null,
        [Description("返回条数，1-50，默认 5")] int? top = null,
        [Description("相似度阈值（0-1，含），低于阈值的命中项被丢弃")] double? minScore = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                throw new BusinessException("请输入查询文本.") { StatusCode = 400 };
            }

            if (minScore.HasValue && (minScore < 0 || minScore > 1))
            {
                throw new BusinessException("相似度阈值必须在 0-1 之间.") { StatusCode = 400 };
            }

            var graph = await RequireGraphAsync(kgId, cancellationToken);
            var caller = RequireCaller();

            var response = await _mediator.Send(new QueryExternalKgRecallCommand
            {
                Caller = caller,
                KnowledgeGraphId = graph.Id,
                Query = queryText.Trim(),
                Top = Math.Clamp(top ?? 5, 1, MaxPageSize),
                MinScore = minScore,
            }, cancellationToken);

            return new KnowledgeGraphMcpRecallResult
            {
                KgId = response.KnowledgeGraphId,
                KgName = response.KnowledgeGraphName,
                Query = response.Query,
                Items = response.Items.Select(x => new KnowledgeGraphMcpRecallItem
                {
                    NodeId = x.NodeId,
                    Name = x.Name,
                    Description = x.Description,
                    EntityTypeName = x.EntityTypeName,
                    Score = x.Score,
                    Neighbors = x.Neighbors.Select(n => new KnowledgeGraphMcpNeighbor
                    {
                        RelationName = n.RelationName,
                        Direction = n.Direction,
                        Name = n.Name,
                        Description = n.Description,
                    }).ToList(),
                }).ToList(),
                SkippedHint = response.SkippedHint,
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 统一执行工具主体：业务异常转为带说明的错误文本（MCP 客户端将文本交给模型），
    /// 其余异常按原样抛出由 SDK 包装为 isError 结果.
    /// </summary>
    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await action();
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning("知识图谱 MCP 工具调用被拒绝：{Message}", ex.Message);
            throw new McpException(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "知识图谱 MCP 工具调用发生未预期异常.");
            throw new McpException("知识图谱 MCP 工具调用失败，请稍后重试.");
        }
    }

    /// <summary>
    /// 读取外部调用方身份（ExternalAuthenticationMiddleware 已完成 key/token 鉴权并写入 HttpContext）.
    /// </summary>
    private ExternalGraphCaller RequireCaller()
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new McpException("MCP 调用缺少 HTTP 上下文.");
        var tokenContext = httpContext.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
        if (tokenContext == null)
        {
            throw new McpException("接入凭证无效，请在请求头携带团队/应用接入 key.");
        }

        return new ExternalGraphCaller { TeamId = tokenContext.TeamId, AccessAppId = tokenContext.AccessAppId };
    }

    /// <summary>
    /// 解析生效图谱：参数 kgId 优先，否则使用接入地址（路由）中的图谱 id，并校验归属团队（404 不泄露存在性）.
    /// </summary>
    private async Task<KnowledgeGraphEntity> RequireGraphAsync(long? kgId, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new McpException("MCP 调用缺少 HTTP 上下文.");
        long effectiveId = kgId ?? ParseRouteKgId(httpContext);
        if (effectiveId <= 0)
        {
            throw new BusinessException("请指定知识图谱 id.") { StatusCode = 400 };
        }

        var caller = RequireCaller();
        return await _externalAuthorizer.AuthorizeAsync(effectiveId, caller.TeamId, write: false, cancellationToken);
    }

    private static long ParseRouteKgId(HttpContext httpContext)
    {
        if (httpContext.Request.RouteValues.TryGetValue("kgId", out var value) && long.TryParse(value?.ToString(), out var kgId))
        {
            return kgId;
        }

        return 0;
    }
}
