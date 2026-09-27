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
/// <inheritdoc cref="QueryKnowledgeGraphRecallTestCommand"/>
/// 复用图检索服务（向量召回 + 一跳扩展）；AI 优化/回答语义与知识库召回测试保持一致.
/// </summary>
public class QueryKnowledgeGraphRecallTestCommandHandler : IRequestHandler<QueryKnowledgeGraphRecallTestCommand, QueryKnowledgeGraphRecallTestCommandResponse>
{
    private const string OptimizePrompt = "请将用户的问题优化为适合在知识图谱中检索的简洁查询文本，去除多余的描述和礼貌用语，确保准确反映用户想查找的实体或关系，只返回优化后的查询文本，不要输出其他内容。";

    private const string AnswerPromptTemplate = """
仅根据下述知识图谱中的事实（实体及其关系），给出全面、详细的回答。
您不知道知识的来源，只需回答。
如果没有足够的信息，请回复“未找到信息”。
问题：{0}
======
事实：
{1}
""";

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IGraphSearchService _graphSearchService;
    private readonly IAiChatCompletionService _chatCompletionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphRecallTestCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="graphSearchService">图检索服务.</param>
    /// <param name="chatCompletionService">一次性对话服务.</param>
    public QueryKnowledgeGraphRecallTestCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IGraphSearchService graphSearchService, IAiChatCompletionService chatCompletionService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _graphSearchService = graphSearchService;
        _chatCompletionService = chatCompletionService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphRecallTestCommandResponse> Handle(QueryKnowledgeGraphRecallTestCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持向量检索.") { StatusCode = 409 };
        }

        // Guid? 判空用 == null，禁止 == Guid.Empty 哨兵（对齐 GraphSearchService）
        if (graph.EmbeddingModelId == null || graph.EmbeddingDimensions <= 0)
        {
            throw new BusinessException("该图谱未配置向量化模型，请先在图谱设置中配置.") { StatusCode = 409 };
        }

        var needChat = request.IsOptimizeQuery || request.IsAnswer;
        var (chatModel, chatChannel) = needChat
            ? await ResolveChatModelAsync(request.AiModelId!.Value, graph.TeamId, cancellationToken)
            : (null, null);

        var optimizedQuery = string.Empty;
        var searchQuery = request.Query;
        if (request.IsOptimizeQuery)
        {
            optimizedQuery = (await _chatCompletionService.CompleteTextAsync(
                chatModel!,
                chatChannel!,
                $"{OptimizePrompt}\n\n用户问题：{request.Query}",
                new AiChatCompletionOptions
                {
                    DisableThinking = true,
                    MaxOutputTokens = 200,
                    Temperature = 0f,
                },
                cancellationToken)).Trim();
            if (!string.IsNullOrWhiteSpace(optimizedQuery))
            {
                searchQuery = optimizedQuery;
            }
        }

        var result = await _graphSearchService.SearchAsync([graph.Id], searchQuery, request.Top, request.MinScore, cancellationToken);

        var answer = string.Empty;
        if (request.IsAnswer && result.Hits.Count > 0)
        {
            var prompt = string.Format(
                AnswerPromptTemplate,
                request.Query,
                result.Text);
            answer = (await _chatCompletionService.CompleteTextAsync(
                chatModel!,
                chatChannel!,
                prompt,
                new AiChatCompletionOptions
                {
                    // RAG 回答无需思维链：禁用思考避免推理型模型把输出预算耗在 reasoning 上导致正文为空
                    DisableThinking = true,
                    MaxOutputTokens = 1024,
                    Temperature = 0.3f,
                },
                cancellationToken)).Trim();
        }

        return new QueryKnowledgeGraphRecallTestCommandResponse
        {
            Query = request.Query,
            OptimizedQuery = optimizedQuery,
            Answer = answer,
            Hits = result.Hits.Select(x => new QueryKnowledgeGraphSearchItem
            {
                KgId = x.KgId,
                NodeId = x.NodeId,
                Name = x.Name,
                Description = x.Description,
                EntityTypeId = x.EntityTypeId,
                EntityTypeName = x.EntityTypeName,
                Score = x.Score,
                Neighbors = x.Neighbors.Select(n => new QueryKnowledgeGraphSearchNeighborItem
                {
                    RelationName = n.RelationName,
                    Direction = n.Direction,
                    Name = n.Name,
                    Description = n.Description,
                }).ToList(),
            }).ToList(),
            SkippedHints = result.SkippedHints.ToList(),
        };
    }

    /// <summary>
    /// 解析团队可用的对话模型（与知识库召回测试同语义：双 Enabled + 公共或团队授权 + conversation 类型）.
    /// </summary>
    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveChatModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var model = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (model == null)
        {
            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
            if (authorized)
            {
                model = candidates.FirstOrDefault();
            }
        }

        if (model == null)
        {
            throw new BusinessException("AI 对话模型不存在、未启用或未授权给你的团队.") { StatusCode = 404 };
        }

        if (!string.Equals(model.m.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是对话模型，无法用于 AI 优化或回答.") { StatusCode = 400 };
        }

        return (model.m, model.c);
    }
}
