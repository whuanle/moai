using MediatR;
using MoAI.Common.Queries.Response;
using MoAI.Infra.Models;

namespace MoAI.Common.Queries;

/// <summary>
/// 查询当前用户是否管理员.
/// </summary>
public class QueryUserIsAdminCommand : IUserIdContext, IRequest<QueryUserIsAdminCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }
}
