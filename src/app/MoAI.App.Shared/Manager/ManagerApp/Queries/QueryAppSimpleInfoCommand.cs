using MediatR;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ManagerApp.Queries;

/// <summary>
/// 查询应用简单信息.
/// </summary>
public class QueryAppSimpleInfoCommand : IRequest<QueryAppSimpleInfoCommandResponse>
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }
}
