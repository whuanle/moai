using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Models;

/// <summary>
/// 查询插件类型.
/// </summary>
public class QueryWikiPluginConfigListCommandResponse
{
    /// <summary>
    /// 爬虫配置列表.
    /// </summary>
    public IReadOnlyCollection<WikiPluginConfigSimpleItem> Items { get; init; } = Array.Empty<WikiPluginConfigSimpleItem>();
}
