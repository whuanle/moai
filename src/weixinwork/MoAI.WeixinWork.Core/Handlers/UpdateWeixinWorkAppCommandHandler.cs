using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.WeixinWork.Commands;
using MoAI.WeixinWork.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.WeixinWork.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateWeixinWorkAppCommand"/>
/// </summary>
public class UpdateWeixinWorkAppCommandHandler : IRequestHandler<UpdateWeixinWorkAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly WeixinWorkAccessTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWeixinWorkAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="tokenProvider">企业微信 access_token 提供者.</param>
    public UpdateWeixinWorkAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, WeixinWorkAccessTokenProvider tokenProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWeixinWorkAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WeixinWorkApps
            .FirstOrDefaultAsync(x => x.Id == request.WeixinWorkAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("企业微信应用连接不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理企业微信应用连接.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.WeixinWorkApps
            .AnyAsync(x => x.TeamId == entity.TeamId && x.Id != entity.Id && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("连接名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        // 同一企业（CorpId）的同一自建应用（AgentId）全局唯一，CorpId/AgentId 可编辑（填错凭证无需删除重建）
        var appExist = await _databaseContext.WeixinWorkApps
            .AnyAsync(x => x.CorpId == request.CorpId && x.AgentId == request.AgentId && x.Id != entity.Id, cancellationToken);

        if (appExist)
        {
            throw new BusinessException("该企业微信应用已创建过连接.") { StatusCode = 409 };
        }

        entity.Name = request.Name;
        entity.Description = request.Description ?? string.Empty;
        entity.CorpId = request.CorpId;
        entity.AgentId = request.AgentId;
        entity.ApiEndpoint = request.ApiEndpoint?.Trim().TrimEnd('/') ?? string.Empty;
        entity.IsDisable = request.IsDisable;

        if (!string.IsNullOrWhiteSpace(request.CorpSecret))
        {
            entity.CorpSecret = request.CorpSecret;
        }

        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            entity.Token = request.Token;
        }

        if (!string.IsNullOrWhiteSpace(request.EncodingAesKey))
        {
            entity.EncodingAesKey = request.EncodingAesKey;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 凭证可能已变化，立即失效 access_token 缓存
        _tokenProvider.Invalidate(entity.Id);

        return EmptyCommandResponse.Default;
    }
}
