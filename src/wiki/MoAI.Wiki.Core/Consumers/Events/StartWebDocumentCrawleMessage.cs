namespace MoAI.Wiki.Consumers.Events;

public class StartWebDocumentCrawleMessage
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 web 配置 id.
    /// </summary>
    public int WebConfigId { get; init; }
}
