using MediatR;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ManagerApp.Queries;

/// <summary>
/// 查询应用详细信息.
/// </summary>
public class QueryAppDetailInfoCommand : IRequest<QueryAppDetailInfoCommandResponse>
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }
}
