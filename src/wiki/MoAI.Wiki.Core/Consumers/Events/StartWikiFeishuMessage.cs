namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// StartWikiFeishuMessage.
/// </summary>
public class StartWikiFeishuMessage
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 web 配置 id.
    /// </summary>
    public int ConfigId { get; init; }
}
