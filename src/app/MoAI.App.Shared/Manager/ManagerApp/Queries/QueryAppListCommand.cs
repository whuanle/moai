using MediatR;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ManagerApp.Queries;

/// <summary>
/// 查询团队下的应用列表.
/// </summary>
public class QueryAppListCommand : IRequest<QueryAppListCommandResponse>
{
    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否外部应用，null表示全部.
    /// </summary>
    public bool? IsForeign { get; init; }
}
