using Maomi;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Storage.Queries;
using MoAI.Wiki.Documents.Queries;
using MoAI.Wiki.Plugins.Mcp.Models;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace MoAI.Wiki.Mcp;

/// <summary>
/// MCP 服务器.
/// </summary>
[InjectOnScoped]
[McpServerToolType]
public class McpWikiToolServer
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpWikiToolServer"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="httpContextAccessor"></param>
    public McpWikiToolServer(DatabaseContext databaseContext, IMediator mediator, IHttpContextAccessor httpContextAccessor)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// MCP 验证.
    /// </summary>
    /// <param name="serverOptions"></param>
    /// <param name="httpContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task ConfigureWikiSpecificOptions(
        McpServerOptions serverOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.RouteValues.TryGetValue("wikiId", out var wikiIdValue))
        {
            return;
        }

        if (!httpContext.Request.Query.TryGetValue("key", out var key))
        {
            return;
        }

        if (!int.TryParse(wikiIdValue?.ToString(), out var wikiId))
        {
            return;
        }

        // 根据 wikiId 获取对应的配置
        var wikiPluginConfig = await _databaseContext.WikiPluginConfigs.Where(x => x.WikiId == wikiId && x.PluginType == "mcp")
            .Select(x => x.Config).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(wikiPluginConfig))
        {
            throw new BusinessException($"No MCP configuration found for wikiId: {wikiId}");
        }

        var config = wikiPluginConfig.JsonToObject<WikiMcpConfig>();

        if (config == null || !config.IsEnable || !string.Equals(config.Key, key, StringComparison.Ordinal))
        {
            throw new BusinessException($"No MCP configuration found for wikiId: {wikiId}");
        }

        var wikiEntity = await _databaseContext.Wikis.Where(x => x.Id == wikiId)
            .Select(x => new QueryWikiInfoResponse
            {
                AvatarKey = x.Avatar,
                Description = x.Description,
                Name = x.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (wikiEntity == null)
        {
            throw new BusinessException($"Wiki not found for wikiId: {wikiId}");
        }

        await _mediator.Send(new QueryAvatarUrlCommand { Items = new[] { wikiEntity }, ExpiryDuration = TimeSpan.FromMinutes(30) });

        // 配置服务器信息
        serverOptions.ServerInfo = new Implementation
        {
            Name = wikiEntity.Name,
            Description = wikiEntity.Description,
            Version = "1.0.0",
            Title = "MoAI Wiki MCP Server",
            Icons = new[]
            {
                new Icon
                {
                    Source = wikiEntity.Avatar,
                }
            }
        };

        serverOptions.ServerInstructions = $"MCP server for wiki: {wikiId}";
    }

    /// <summary>
    /// 搜索.
    /// </summary>
    /// <param name="requestContext"></param>
    /// <param name="query"></param>
    /// <param name="isAnswer"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [McpServerTool(Name = "search")]
    [Description("在知识库中搜索内容")]
    public async Task<string> SearchAsync(
        RequestContext<CallToolRequestParams> requestContext,
        [Description("提问")] string query,
        [Description("是否需要 ai 回答整理问题")] bool isAnswer,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        if (!httpContext.Request.RouteValues.TryGetValue("wikiId", out var wikiIdValue))
        {
            return "未指定知识库";
        }

        if (!int.TryParse(wikiIdValue?.ToString(), out var wikiId))
        {
            return "未指定知识库";
        }

        // 查询知识库被授权的 chat 模型，如果没有再查公开的
        var aiModels = await _mediator.Send(new QueryTeamViewAiModelListCommand
        {
            AiModelType = AI.Models.AiModelType.Chat,
            WikiId = wikiId,
        });

        if (aiModels == null)
        {
            throw new BusinessException("该知识库无可用模型");
        }

        var aiModel = aiModels.AiModels.Where(x => x.IsAuthorization).FirstOrDefault();
        if (aiModel == null)
        {
            aiModel = aiModels.AiModels.FirstOrDefault();
        }

        var results = await _mediator.Send(new SearchWikiDocumentTextCommand
        {
            ContextUserId = 0,
            ContextUserType = Infra.Models.UserType.ExternalApp,
            WikiId = wikiId,
            AiModelId = aiModel!.Id,
            IsAnswer = isAnswer,
            IsOptimizeQuery = true,
            Limit = 20,
            Query = query,
        });

        StringBuilder stringBuilder = new();
        foreach (var item in results.SearchResult.DistinctBy(x => x.SourceChunkId))
        {
            stringBuilder.AppendLine(item.ChunkText);
        }

        return stringBuilder.ToString();
    }
}
