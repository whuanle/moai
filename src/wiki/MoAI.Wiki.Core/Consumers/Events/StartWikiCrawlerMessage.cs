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
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}