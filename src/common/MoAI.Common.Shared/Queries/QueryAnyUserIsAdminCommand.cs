using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

/// <summary>
/// 查询列表中是否有用户是管理员.
/// </summary>
public class QueryAnyUserIsAdminCommand : IRequest<SimpleBool>
{
    /// <summary>
    /// 用户 id 列表.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();
}