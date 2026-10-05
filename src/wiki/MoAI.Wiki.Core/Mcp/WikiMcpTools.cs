using System.ComponentModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.External;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;
using MoAI.Wiki.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MoAI.Wiki.Mcp;

/// <summary>
/// 知识库 MCP 工具集：以团队/应用接入 key（ExternalAuthenticationMiddleware 已完成鉴权）访问团队知识库，
/// 仅暴露只读能力（知识库列表 / 文档搜索 / 向量召回）。SDK 对每次工具调用构造一个实例，
/// 构造发生在请求 DI 作用域内（已在开发环境用请求作用域比对验证），可安全注入 scoped 服务.
/// </summary>
[McpServerToolType]
public class WikiMcpTools
{
    private const int MaxPageSize = 50;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMediator _mediator;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly ILogger<WikiMcpTools> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiMcpTools"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP 上下文访问器（读取鉴权上下文与路由 wikiId）.</param>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="logger">日志.</param>
    public WikiMcpTools(IHttpContextAccessor httpContextAccessor, IMediator mediator, IExternalWikiAuthorizer externalWikiAuthorizer, ILogger<WikiMcpTools> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _mediator = mediator;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _logger = logger;
    }

    /// <summary>
    /// 获取接入凭证所属团队下的知识库列表（含文档数量与切片数量）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回知识库列表.</returns>
    [McpServerTool(Name = "list_knowledge_bases", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("获取当前团队有权限访问的知识库列表，包含每个知识库的名称、描述、文档数量与切片数量。")]
    public async Task<WikiMcpKnowledgeBaseList> ListKnowledgeBasesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var caller = RequireCaller();
            var response = await _mediator.Send(new QueryExternalWikisCommand { Caller = caller }, cancellationToken);
            return new WikiMcpKnowledgeBaseList
            {
                TeamId = response.TeamId,
                Items = response.Items.Select(x => new WikiMcpKnowledgeBase
                {
                    WikiId = x.WikiId,
                    Name = x.Name,
                    Description = x.Description,
                    DocumentCount = x.DocumentCount,
                    ChunkCount = x.ChunkCount,
                    CreateTime = x.CreateTime,
                    LastDocumentUpdateTime = x.LastDocumentUpdateTime,
                }).ToList(),
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 按名称关键字搜索知识库内的文档文件（分页）.
    /// </summary>
    /// <param name="query">名称关键字，空串返回全部文档.</param>
    /// <param name="wikiId">知识库 id；不传时使用接入地址中的知识库 id.</param>
    /// <param name="pageNo">页码，从 1 开始，默认 1.</param>
    /// <param name="pageSize">每页数量，1-50，默认 20.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回文档搜索结果.</returns>
    [McpServerTool(Name = "search_knowledge_base_files", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("按文件名关键字搜索指定知识库内的文档，返回文档 id、名称、类型、大小、向量化状态与切片数量，支持分页。可先用 list_knowledge_bases 获取知识库 id。")]
    // 注意：SDK 把无默认值的参数一律按必填校验，可选参数必须带 = null 默认值
    public async Task<WikiMcpDocumentSearchResult> SearchKnowledgeBaseFilesAsync(
        [Description("文件名关键字，不传或空串表示返回全部文档")] string? query = null,
        [Description("知识库 id；不传时使用接入地址中的知识库 id")] long? wikiId = null,
        [Description("页码，从 1 开始")] int? pageNo = null,
        [Description("每页数量，1-50")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var caller = RequireCaller();
            var wiki = await RequireWikiAsync(wikiId, cancellationToken);

            var response = await _mediator.Send(new QueryExternalWikiDocumentsCommand
            {
                Caller = caller,
                WikiId = wiki.Id,
                Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
                PageNo = Math.Max(pageNo ?? 1, 1),
                PageSize = Math.Clamp(pageSize ?? 20, 1, MaxPageSize),
            }, cancellationToken);

            return new WikiMcpDocumentSearchResult
            {
                WikiId = wiki.Id,
                WikiName = wiki.Name,
                Keyword = query,
                Total = response.Total,
                PageNo = response.PageNo,
                PageSize = response.PageSize,
                Items = response.Items.Select(x => new WikiMcpDocument
                {
                    DocumentId = x.DocumentId,
                    FileName = x.FileName,
                    FileType = Path.GetExtension(x.FileName),
                    FileSize = x.FileSize,
                    IsEmbedding = x.IsEmbedding,
                    ChunkCount = x.ChunkCount,
                    UpdateTime = x.UpdateTime,
                }).ToList(),
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 在知识库内做向量召回，返回与查询最相关的资料切片.
    /// </summary>
    /// <param name="queryText">查询文本.</param>
    /// <param name="wikiId">知识库 id；不传时使用接入地址中的知识库 id.</param>
    /// <param name="top">返回条数，1-50，默认 5.</param>
    /// <param name="minScore">相似度阈值（0-1），低于阈值的命中项被丢弃，不传表示不过滤.</param>
    /// <param name="documentIds">限定检索的文档 id 集合，不传表示全部文档.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回召回切片集合.</returns>
    [McpServerTool(Name = "search_knowledge_base_recall", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("在指定知识库内按语义向量召回与查询最相关的资料切片（内容片段 + 相似度得分），适合回答问题前检索团队资料。可先用 list_knowledge_bases 获取知识库 id，用 search_knowledge_base_files 查看文档列表。")]
    public async Task<WikiMcpRecallResult> SearchKnowledgeBaseRecallAsync(
        [Description("查询文本，例如要了解的问题")] string queryText,
        [Description("知识库 id；不传时使用接入地址中的知识库 id")] long? wikiId = null,
        [Description("返回条数，1-50，默认 5")] int? top = null,
        [Description("相似度阈值（0-1，含），低于阈值的命中项被丢弃")] double? minScore = null,
        [Description("限定检索的文档 id 集合（可先用 search_knowledge_base_files 查询），不传表示全部文档")] IReadOnlyCollection<long>? documentIds = null,
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

            var wiki = await RequireWikiAsync(wikiId, cancellationToken);
            var caller = RequireCaller();

            var response = await _mediator.Send(new QueryExternalWikiRecallCommand
            {
                Caller = caller,
                WikiId = wiki.Id,
                Query = queryText.Trim(),
                Top = Math.Clamp(top ?? 5, 1, MaxPageSize),
                MinScore = minScore,
                DocumentIds = documentIds ?? Array.Empty<long>(),
            }, cancellationToken);

            return new WikiMcpRecallResult
            {
                WikiId = response.WikiId,
                WikiName = response.WikiName,
                Query = response.Query,
                Items = response.Items.Select(x => new WikiMcpRecallItem
                {
                    DocumentId = x.DocumentId,
                    DocumentName = x.DocumentName,
                    ChunkId = x.ChunkId,
                    ChunkIndex = x.ChunkIndex,
                    DocumentChunkCount = x.DocumentChunkCount,
                    ContentType = MetadataTypeLabel(x.MetadataType),
                    Content = x.Content,
                    Score = x.Score,
                    RerankScore = x.RerankScore,
                    Context = x.Context.Select(c => new WikiMcpRecallContextChunk
                    {
                        ChunkIndex = c.ChunkIndex,
                        Content = c.Content,
                    }).ToList(),
                }).ToList(),
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
            _logger.LogWarning("知识库 MCP 工具调用被拒绝：{Message}", ex.Message);
            throw new McpException(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "知识库 MCP 工具调用发生未预期异常.");
            throw new McpException("知识库 MCP 工具调用失败，请稍后重试.");
        }
    }

    /// <summary>
    /// 读取外部调用方身份（ExternalAuthenticationMiddleware 已完成 key/token 鉴权并写入 HttpContext）.
    /// </summary>
    private ExternalWikiCaller RequireCaller()
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new McpException("MCP 调用缺少 HTTP 上下文.");
        var tokenContext = httpContext.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
        if (tokenContext == null)
        {
            throw new McpException("接入凭证无效，请在请求头携带团队/应用接入 key.");
        }

        return new ExternalWikiCaller { TeamId = tokenContext.TeamId, AccessAppId = tokenContext.AccessAppId };
    }

    /// <summary>
    /// 解析生效知识库：参数 wikiId 优先，否则使用接入地址（路由）中的知识库 id，并校验归属团队.
    /// </summary>
    private async Task<WikiEntity> RequireWikiAsync(long? wikiId, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new McpException("MCP 调用缺少 HTTP 上下文.");
        long effectiveId = wikiId ?? ParseRouteWikiId(httpContext);
        if (effectiveId <= 0)
        {
            throw new BusinessException("请指定知识库 id.") { StatusCode = 400 };
        }

        var caller = RequireCaller();
        return await _externalWikiAuthorizer.AuthorizeAsync(effectiveId, caller.TeamId, cancellationToken);
    }

    private static long ParseRouteWikiId(HttpContext httpContext)
    {
        if (httpContext.Request.RouteValues.TryGetValue("wikiId", out var value) && long.TryParse(value?.ToString(), out var wikiId))
        {
            return wikiId;
        }

        return 0;
    }

    /// <summary>
    /// 元数据类型转可读标签（与召回测试的前端约定一致）.
    /// </summary>
    private static string MetadataTypeLabel(int metadataType)
    {
        return metadataType switch
        {
            0 => "source",
            1 => "outline",
            2 => "question",
            3 => "keyword",
            4 => "summary",
            5 => "aggregated",
            _ => $"metadata_{metadataType}",
        };
    }
}
