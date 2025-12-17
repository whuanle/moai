using MediatR;
using MoAI.Wiki.Plugins.Feishu.Models;

namespace MoAI.Wiki.Plugins.Feishu.Queries;

/// <summary>
/// 查询已经配置的爬虫插件实例列表.
/// </summary>
public class QueryWikiFeishuPluginConfigListCommand : IRequest<QueryWikiFeishuPluginConfigListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }
}