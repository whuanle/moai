using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateTeamResourceKeyCommand"/>
/// </summary>
public class CreateTeamResourceKeyCommandHandler : IRequestHandler<CreateTeamResourceKeyCommand, CreateTeamResourceKeyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamResourceKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateTeamResourceKeyCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<CreateTeamResourceKeyCommandResponse> Handle(CreateTeamResourceKeyCommand request, CancellationToken cancellationToken)
    {
        await EnsureTeamAdminAsync(request.TeamId, request.ContextUserId, cancellationToken);

        var teamId = (int)request.TeamId;

        // 资源必须存在、归属团队，且已开启对应对外功能（未开启功能不生成 key）
        switch (request.ResourceType)
        {
            case TeamResourceType.App:
                if (!Guid.TryParse(request.ResourceId, out var appId))
                {
                    throw new BusinessException("应用 id 不正确.") { StatusCode = 400 };
                }

                var app = await _databaseContext.Apps
                    .Where(x => x.Id == appId)
                    .Select(x => new { x.TeamId, x.IsAcpA2aEnabled })
                    .FirstOrDefaultAsync(cancellationToken);
                if (app == null || app.TeamId != teamId)
                {
                    throw new BusinessException("应用不存在.") { StatusCode = 404 };
                }

                if (!app.IsAcpA2aEnabled)
                {
                    throw new BusinessException("应用未开启对外协议接入（ACP/A2A），无需生成资源 key.") { StatusCode = 400 };
                }

                break;
            case TeamResourceType.Wiki:
                var wiki = await _databaseContext.Wikis
                    .Where(x => x.Id == long.Parse(request.ResourceId))
                    .Select(x => new { x.TeamId, x.IsMcpEnabled })
                    .FirstOrDefaultAsync(cancellationToken);
                if (wiki == null || wiki.TeamId != teamId)
                {
                    throw new BusinessException("知识库不存在.") { StatusCode = 404 };
                }

                if (!wiki.IsMcpEnabled)
                {
                    throw new BusinessException("知识库未开启 MCP 功能，无需生成资源 key.") { StatusCode = 400 };
                }

                break;
            case TeamResourceType.KnowledgeGraph:
                var kg = await _databaseContext.KnowledgeGraphs
                    .Where(x => x.Id == long.Parse(request.ResourceId))
                    .Select(x => new { x.TeamId, x.IsMcpEnabled })
                    .FirstOrDefaultAsync(cancellationToken);
                if (kg == null || kg.TeamId != teamId)
                {
                    throw new BusinessException("知识图谱不存在.") { StatusCode = 404 };
                }

                if (!kg.IsMcpEnabled)
                {
                    throw new BusinessException("知识图谱未开启 MCP 功能，无需生成资源 key.") { StatusCode = 400 };
                }

                break;
            default:
                throw new BusinessException("资源类型不正确.") { StatusCode = 400 };
        }

        var (entity, created) = await TeamResourceKeyEnsurer.EnsureAsync(_databaseContext, teamId, request.ResourceType, request.ResourceId, cancellationToken);
        return new CreateTeamResourceKeyCommandResponse
        {
            TeamResourceKeyId = entity.Id,
            ResourceType = entity.ResourceType,
            ResourceId = entity.ResourceId,
            Key = entity.Key,
            KeyPrefix = entity.KeyPrefix,
            Created = created,
        };
    }

    private async Task EnsureTeamAdminAsync(long teamId, long contextUserId, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, contextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理资源 key.") { StatusCode = 403 };
        }
    }
}

/// <summary>
/// <inheritdoc cref="QueryTeamResourceKeysCommand"/>
/// </summary>
public class QueryTeamResourceKeysCommandHandler : IRequestHandler<QueryTeamResourceKeysCommand, QueryTeamResourceKeysCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamResourceKeysCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTeamResourceKeysCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamResourceKeysCommandResponse> Handle(QueryTeamResourceKeysCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以查看资源 key.") { StatusCode = 403 };
        }

        var items = await _databaseContext.TeamResourceKeys
            .Where(x => x.TeamId == (int)request.TeamId)
            .OrderBy(x => x.ResourceType).ThenBy(x => x.ResourceId)
            .Select(x => new TeamResourceKeyItem
            {
                TeamResourceKeyId = x.Id,
                TeamId = x.TeamId,
                ResourceType = x.ResourceType,
                ResourceId = x.ResourceId,
                Key = x.Key,
                KeyPrefix = x.KeyPrefix,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryTeamResourceKeysCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items,
        };
    }
}

/// <summary>
/// <inheritdoc cref="ResetTeamResourceKeyCommand"/>
/// </summary>
public class ResetTeamResourceKeyCommandHandler : IRequestHandler<ResetTeamResourceKeyCommand, ResetTeamResourceKeyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetTeamResourceKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public ResetTeamResourceKeyCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<ResetTeamResourceKeyCommandResponse> Handle(ResetTeamResourceKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.TeamResourceKeys
            .FirstOrDefaultAsync(x => x.Id == request.TeamResourceKeyId, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("资源 key 不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理资源 key.") { StatusCode = 403 };
        }

        // 同表行重新生成 key 值：行 id 不变（直连身份延续），旧 key 立即失效
        var (key, keyPrefix) = TeamResourceKeyGenerator.New();
        entity.Key = key;
        entity.KeyPrefix = keyPrefix;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new ResetTeamResourceKeyCommandResponse
        {
            TeamResourceKeyId = entity.Id,
            Key = key,
            KeyPrefix = keyPrefix,
        };
    }
}
