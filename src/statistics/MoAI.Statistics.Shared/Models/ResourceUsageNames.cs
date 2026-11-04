namespace MoAI.Statistics.Services;

/// <summary>
/// 资源使用统计的资源类型名常量，一个 name 对应一个计数器（Redis hash）与一个写入器.
/// </summary>
public static class ResourceUsageNames
{
    /// <summary>
    /// 插件（自定义/动态/静态共用 Plugins 表主键）.
    /// </summary>
    public const string Plugin = "plugin-usage";

    /// <summary>
    /// 应用.
    /// </summary>
    public const string App = "app-usage";

    /// <summary>
    /// 应用消息（会话落库的物理消息行数增量，value 可为负；删除会话不回减）.
    /// </summary>
    public const string AppMessage = "app-message-usage";

    /// <summary>
    /// 技能.
    /// </summary>
    public const string Skill = "skill-usage";

    /// <summary>
    /// 提示词（专家提示词被选用）.
    /// </summary>
    public const string Prompt = "prompt-usage";

    /// <summary>
    /// 知识库.
    /// </summary>
    public const string Wiki = "wiki-usage";

    /// <summary>
    /// AI 模型用量（value 为 token 数，复合 key 见 AiModelUsageCounterKey）.
    /// </summary>
    public const string AiModel = "ai-model-usage";
}
