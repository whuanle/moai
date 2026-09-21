using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiRecallTestCommand"/>
/// </summary>
public class QueryWikiRecallTestCommandHandler : IRequestHandler<QueryWikiRecallTestCommand, QueryWikiRecallTestCommandResponse>
{
    private const string OptimizePrompt = "请将用户的问题优化为适合在知识库中搜索的简洁查询文本，去除多余的描述和礼貌用语，确保准确反映用户的查询意图，只返回优化后的查询文本，不要输出其他内容。";

    private const string AnswerPromptTemplate = """
仅根据下述事实，给出全面、详细的回答。
您不知道知识的来源，只需回答。
如果没有足够的信息，请回复“未找到信息”。
问题：{0}
======
事实：
{1}
""";

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IWikiSearchService _wikiSearchService;
    private readonly IAiChatCompletionService _chatCompletionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiRecallTestCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="wikiSearchService">知识库检索服务.</param>
    /// <param name="chatCompletionService">一次性对话服务.</param>
    public QueryWikiRecallTestCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IWikiSearchService wikiSearchService,
        IAiChatCompletionService chatCompletionService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _wikiSearchService = wikiSearchService;
        _chatCompletionService = chatCompletionService;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiRecallTestCommandResponse> Handle(QueryWikiRecallTestCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var needChat = request.IsOptimizeQuery || request.IsAnswer;
        var (chatModel, chatChannel) = needChat
            ? await ResolveChatModelAsync(request.AiModelId!.Value, wiki.TeamId, cancellationToken)
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

        var hits = await _wikiSearchService.SearchInWikiAsync(
            wiki.Id,
            searchQuery,
            request.Top,
            request.MinScore,
            request.DocumentIds.Count > 0 ? request.DocumentIds : null,
            cancellationToken);

        var answer = string.Empty;
        if (request.IsAnswer && hits.Count > 0)
        {
            var prompt = string.Format(
                AnswerPromptTemplate,
                request.Query,
                string.Join("\n", hits.Select(x => x.Content).Distinct()));
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

        return new QueryWikiRecallTestCommandResponse
        {
            Query = request.Query,
            OptimizedQuery = optimizedQuery,
            Answer = answer,
            Items = hits.Select(x => new QueryWikiRecallTestItem
            {
                DocumentId = x.DocumentId,
                DocumentName = x.DocumentName,
                ChunkId = x.ChunkId,
                MetadataType = x.MetadataType,
                Content = x.Content,
                Score = x.Score ?? 0,
            }).ToList(),
        };
    }

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
