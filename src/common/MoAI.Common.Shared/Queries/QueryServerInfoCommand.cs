using MediatR;
using MoAI.Common.Queries.Response;

namespace MoAI.Common.Queries;

/// <summary>
/// 查询服务端公开配置信息.
/// </summary>
public class QueryServerInfoCommand : IRequest<QueryServerInfoCommandResponse>
{
}
