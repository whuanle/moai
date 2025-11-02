using MediatR;
using MoAI.Admin.OAuth.Queries.Responses;

namespace MoAI.Login.Queries;

/// <summary>
/// 获取所有 OAuth 提供者详细信息。
/// </summary>
public class QueryAllOAuthPrividerDetailCommand : IRequest<QueryAllOAuthPrividerDetailCommandResponse>
{
}
