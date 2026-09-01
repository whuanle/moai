using MediatR;
using MoAI.OauthConnect.Queries.Responses;

namespace MoAI.OauthConnect.Queries;

/// <summary>
/// 查询全部第三方登录连接配置.
/// </summary>
public class QueryAllOAuthConnectionCommand : IRequest<QueryAllOAuthConnectionCommandResponse>
{
}
