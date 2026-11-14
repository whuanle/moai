using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UnfavoriteAppCommand"/>
/// </summary>
public class UnfavoriteAppCommandHandler : IRequestHandler<UnfavoriteAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnfavoriteAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UnfavoriteAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UnfavoriteAppCommand request, CancellationToken cancellationToken)
    {
        // 不校验应用状态：应用下架/停用后仍允许取消收藏（清理遗留收藏）；未收藏幂等返回成功
        var favorite = await _databaseContext.AppFavorites
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);
        if (favorite == null)
        {
            return EmptyCommandResponse.Default;
        }

        // 软删除由框架拦截器转换（IsDeleted=ticks），(app_id, user_id) 唯一索引只作用于活跃行
        _databaseContext.AppFavorites.Remove(favorite);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
