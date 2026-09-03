using MediatR;
using MoAI.Variable.Queries.Responses;

namespace MoAI.Variable.Queries;

/// <summary>
/// 查询变量详情；私密变量的值仅 Owner/Admin 可见（成员访问值返回 403）.
/// </summary>
public class QueryVariableCommand : IRequest<QueryVariableCommandResponse>
{
    /// <summary>
    /// 变量 id.
    /// </summary>
    public long VariableId { get; init; }
}
