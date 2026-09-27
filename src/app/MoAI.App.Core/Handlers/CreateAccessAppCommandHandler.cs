using MediatR;
using MoAI.App.Commands;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAccessAppCommand"/>
/// </summary>
public class CreateAccessAppCommandHandler : IRequestHandler<CreateAccessAppCommand, CreateAccessAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateAccessAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<CreateAccessAppCommandResponse> Handle(CreateAccessAppCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用接入.") { StatusCode = 403 };
        }

        var (secret, keyPrefix) = AccessAppKeyGenerator.New();

        // 不传 scopes 默认外部资源全量（读写+对话+知识图谱，与存量行为/列默认 230 一致），显式空列表=无资源权限（可仅勾 model 直连网关）；
        // handler 侧把关代码合法性（Validate 规则作为绑定直传时的前置校验）
        var scopes = TeamApiKeyScopeCodes.AccessAppDefault;
        if (request.Scopes != null)
        {
            // 空列表合法=纯对话接入；非空列表须全部为应用接入允许代码（model/wiki_read/wiki_write/wiki_mcp）且不重复
            scopes = TeamApiKeyScopes.None;
            if (request.Scopes.Count > 0)
            {
                if (request.Scopes.Count != request.Scopes.Distinct().Count()
                    || !TeamApiKeyScopeCodes.TryParseAccessAppCodes(request.Scopes.Distinct(), out var parsed))
                {
                    throw new BusinessException("功能范围代码不合法.") { StatusCode = 400 };
                }

                scopes = parsed;
            }
        }

        var entity = new AccessAppEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Key = secret,
            Scopes = (int)scopes,
        };

        _databaseContext.AccessApps.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateAccessAppCommandResponse
        {
            AccessAppId = entity.Id,
            Key = secret,
            KeyPrefix = keyPrefix,
        };
    }
}
