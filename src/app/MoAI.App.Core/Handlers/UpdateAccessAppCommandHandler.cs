using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Gateway.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAccessAppCommand"/>
/// </summary>
public class UpdateAccessAppCommandHandler : IRequestHandler<UpdateAccessAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly ExternalKeyCache _keyCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="keyCache">外部接入 key 缓存.</param>
    public UpdateAccessAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, ExternalKeyCache keyCache)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _keyCache = keyCache;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAccessAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.AccessApps.FirstOrDefaultAsync(x => x.Id == request.AccessAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("应用接入不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用接入.") { StatusCode = 403 };
        }

        entity.Name = request.Name;
        entity.Description = request.Description ?? string.Empty;

        // scopes=null 不修改；显式列表（可为空=纯对话接入）整体覆盖，代码合法性同创建口径（model/wiki_read/wiki_write/wiki_mcp）
        if (request.Scopes != null)
        {
            var scopes = TeamApiKeyScopes.None;
            if (request.Scopes.Count > 0
                && request.Scopes.Count == request.Scopes.Distinct().Count()
                && TeamApiKeyScopeCodes.TryParseAccessAppCodes(request.Scopes.Distinct(), out var parsed))
            {
                scopes = parsed;
            }
            else if (request.Scopes.Count > 0)
            {
                throw new BusinessException("功能范围代码不合法.") { StatusCode = 400 };
            }

            entity.Scopes = (int)scopes;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 范围变更删除缓存，key 直连与换 token 立即生效
        await _keyCache.RemoveAccessKeyAsync(ApiKeyGenerator.Hash(entity.Key));
        return EmptyCommandResponse.Default;
    }
}
