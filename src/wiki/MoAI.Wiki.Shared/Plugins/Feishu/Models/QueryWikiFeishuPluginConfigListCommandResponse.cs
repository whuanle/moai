using MediatR;

namespace MoAI.Wiki.Plugins.Feishu.Models;

public class QueryWikiFeishuPluginConfigListCommandResponse
{
    /// <summary>
    /// 配置列表.
    /// </summary>
    public IReadOnlyCollection<WikiFeishuPluginConfigSimpleItem> Items { get; set; } = Array.Empty<WikiFeishuPluginConfigSimpleItem>();
}