using MediatR;

namespace MoAI.Plugin.Public.Queries;

/// <summary>
/// 查询公开使用的插件列表.
/// </summary>
public class QueryPublicPluginListCommand : IRequest<QueryPublicPluginListCommandResponse>
{
}
