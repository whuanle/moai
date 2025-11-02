using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Common.Queries;

/// <summary>
/// 查询用户基本信息的请求.
/// </summary>
public class QueryUserViewUserInfoCommand : IRequest<UserStateInfo>
{
}
