using MediatR;
using MoAI.Common.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 查询管理员列表，返回管理员用户id.
/// </summary>
public class QueryAdminIdsCommand : IRequest<QueryAdminIdsCommandResponse>
{
}
