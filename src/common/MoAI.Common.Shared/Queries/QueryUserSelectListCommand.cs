using MediatR;
using MoAI.Common.Queries.Response;
using MoAI.Infra.Models;

namespace MoAI.Common.Queries;

/// <summary>
/// 懒加载查询用户选择列表，用于下拉选择人员.
/// </summary>
public class QueryUserSelectListCommand : PagedParamter, IRequest<QueryUserSelectListCommandResponse>
{
    /// <summary>
    /// 搜索关键字，支持用户名、昵称模糊搜索.
    /// </summary>
    public string? Search { get; init; }
}
