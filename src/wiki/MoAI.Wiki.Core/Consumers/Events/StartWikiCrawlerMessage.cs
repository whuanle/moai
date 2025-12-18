using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Consumers.Events;

public class StartWikiCrawlerMessage
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 web 配置 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 命令参数.
    /// </summary>
    public StartWikiCrawlerPluginTaskCommand Command { get; init; } = default!;
}