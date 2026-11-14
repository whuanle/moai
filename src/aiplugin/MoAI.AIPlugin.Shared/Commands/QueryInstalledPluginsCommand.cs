using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Commands;

/// <summary>查询 MoAI 本地已安装的在线插件.</summary>
public sealed class QueryInstalledPluginsCommand : IRequest<IReadOnlyList<InstalledPluginItem>>
{
}
