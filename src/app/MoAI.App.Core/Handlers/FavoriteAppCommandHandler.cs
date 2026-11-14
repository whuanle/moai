using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="FavoriteAppCommand"/>
/// </summary>
public class FavoriteAppCommandHandler : IRequestHandler<FavoriteAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IPublicAppScopeService _publicAppScopeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FavoriteAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="publicAppScopeService">应用市场可视范围判定服务.</param>
    public FavoriteAppCommandHandler(DatabaseContext databaseContext, IPublicAppScopeService publicAppScopeService)
    {
        _databaseContext = databaseContext;
        _publicAppScopeService = publicAppScopeService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(FavoriteAppCommand request, CancellationToken cancellationToken)
    {
        // 仅允许收藏应用广场可见的应用：已发布、已公开、未禁用、非外部（与广场列表过滤同口径）
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.AppId && !x.IsExternal && x.IsPublic && !x.IsDisable && x.PublishStatus == 1,
                cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        // 可视范围：无组织授权记录=全员可见，有记录仅范围覆盖用户可收藏（与广场可见性一致，范围外 404 不泄露）
        if (!await _publicAppScopeService.IsAllowedAsync(app.Id, request.ContextUserId, cancellationToken))
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var exists = await _databaseContext.AppFavorites
            .AnyAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);
        if (exists)
        {
            // 幂等：已收藏直接返回成功
            return EmptyCommandResponse.Default;
        }

        _databaseContext.AppFavorites.Add(new AppFavoriteEntity
        {
            Id = Guid.CreateVersion7(),
            AppId = request.AppId,
            UserId = request.ContextUserId
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
