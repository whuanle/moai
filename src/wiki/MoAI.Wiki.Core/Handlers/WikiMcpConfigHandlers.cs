using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// 知识库 MCP 配置响应组装：开关 + 该库的团队资源 key（开启过 MCP 才有）.
/// </summary>
internal static class WikiMcpConfigResponseMapper
{
    /// <summary>
    /// 组装响应.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="wiki">知识库（含开关字段）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回响应模型.</returns>
    public static async Task<WikiMcpConfigResponse> BuildAsync(DatabaseContext databaseContext, Database.Entities.WikiEntity wiki, CancellationToken cancellationToken)
    {
        var response = new WikiMcpConfigResponse
        {
            IsMcpEnabled = wiki.IsMcpEnabled,
        };

        if (wiki.IsMcpEnabled)
        {
            var key = await databaseContext.TeamResourceKeys
                .Where(x => x.TeamId == wiki.TeamId && x.ResourceType == TeamResourceType.Wiki && x.ResourceId == wiki.Id.ToString())
                .Select(x => new { x.Id, x.Key, x.KeyPrefix, x.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (key != null)
            {
                response.TeamResourceKeyId = key.Id;
                response.Key = key.Key;
                response.KeyPrefix = key.KeyPrefix;
                response.Status = key.Status;
            }
        }

        return response;
    }
}

/// <summary>
/// <inheritdoc cref="UpdateWikiMcpConfigCommand"/>
/// </summary>
public class UpdateWikiMcpConfigCommandHandler : IRequestHandler<UpdateWikiMcpConfigCommand, WikiMcpConfigResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiMcpConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public UpdateWikiMcpConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<WikiMcpConfigResponse> Handle(UpdateWikiMcpConfigCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以修改知识库.") { StatusCode = 403 };
        }

        wiki.IsMcpEnabled = request.IsMcpEnabled;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 开启 MCP 即自动生成/复用该库的团队资源 key（查库无则写、有则复用；一个知识库一把）
        if (wiki.IsMcpEnabled)
        {
            await TeamResourceKeyEnsurer.EnsureAsync(_databaseContext, wiki.TeamId, TeamResourceType.Wiki, wiki.Id.ToString(), cancellationToken);
        }

        return await WikiMcpConfigResponseMapper.BuildAsync(_databaseContext, wiki, cancellationToken);
    }
}

/// <summary>
/// <inheritdoc cref="QueryWikiMcpConfigCommand"/>
/// </summary>
public class QueryWikiMcpConfigCommandHandler : IRequestHandler<QueryWikiMcpConfigCommand, WikiMcpConfigResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiMcpConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryWikiMcpConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<WikiMcpConfigResponse> Handle(QueryWikiMcpConfigCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以查看知识库配置.") { StatusCode = 403 };
        }

        return await WikiMcpConfigResponseMapper.BuildAsync(_databaseContext, wiki, cancellationToken);
    }
}
