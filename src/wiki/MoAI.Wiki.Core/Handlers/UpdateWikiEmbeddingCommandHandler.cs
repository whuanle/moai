using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateWikiEmbeddingCommand"/>
/// </summary>
public class UpdateWikiEmbeddingCommandHandler : IRequestHandler<UpdateWikiEmbeddingCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiEmbeddingCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public UpdateWikiEmbeddingCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiEmbeddingCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        await EnsureAdminAsync(wiki, cancellationToken);

        if (wiki.IsLock)
        {
            throw new BusinessException("知识库已有文档向量化，配置已锁定，不能修改.") { StatusCode = 409 };
        }

        await EnsureEmbeddingModelAsync(request.EmbeddingModelId, wiki.TeamId, cancellationToken);

        wiki.EmbeddingModelId = request.EmbeddingModelId;
        wiki.EmbeddingDimensions = request.EmbeddingDimensions;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task EnsureEmbeddingModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == modelId && x.Enabled && x.IsDeleted == 0, cancellationToken);
        if (model == null)
        {
            throw new BusinessException("向量化模型不存在或未启用.") { StatusCode = 404 };
        }

        if (!string.Equals(model.ModelKind, "embedding", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是向量化模型.") { StatusCode = 400 };
        }

        await EnsureAuthorizedAsync(modelId, teamId, cancellationToken);
    }

    private async Task EnsureAuthorizedAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var isPublic = await _databaseContext.AiModels
            .Where(x => x.Id == modelId)
            .Select(x => x.IsPublic)
            .FirstOrDefaultAsync(cancellationToken);
        if (isPublic)
        {
            return;
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
        if (!authorized)
        {
            throw new BusinessException("该模型未授权给你的团队使用.") { StatusCode = 403 };
        }
    }

    private async Task EnsureAdminAsync(WikiEntity wiki, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以修改知识库配置.") { StatusCode = 403 };
        }
    }
}
