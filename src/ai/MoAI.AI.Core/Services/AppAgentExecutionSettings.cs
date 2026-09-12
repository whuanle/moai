using System.Text.Json;

namespace MoAI.AI.Models;

/// <summary>
/// 应用对话执行参数（对应 <c>app_agent_config.execution_settings</c> JSON）.
/// </summary>
public sealed class AppAgentExecutionSettings
{
    /// <summary>
    /// 工具结果压缩触发：消息数超过该值时折叠旧工具结果.
    /// </summary>
    public int ToolResultTriggerMessages { get; set; } = 12;

    /// <summary>
    /// 摘要压缩触发：token 超过该值时对旧对话做摘要.
    /// </summary>
    public int SummarizeTriggerTokens { get; set; } = 4000;

    /// <summary>
    /// 滑动窗口保留的用户轮数.
    /// </summary>
    public int PreserveTurns { get; set; } = 6;

    /// <summary>
    /// 紧急截断的 token 上限.
    /// </summary>
    public int HardTokenLimit { get; set; } = 24000;

    /// <summary>
    /// 是否启用 LLM 摘要压缩（默认关闭，仅做无损压缩）.
    /// </summary>
    public bool EnableSummarization { get; set; }

    /// <summary>
    /// 摘要模型 id（ai_model.id），空则回落到应用对话模型.
    /// </summary>
    public string? SummarizerModelId { get; set; }

    /// <summary>
    /// 沙箱配置；为空表示未开启沙箱.
    /// </summary>
    public SandboxSettings? Sandbox { get; set; }

    /// <summary>
    /// 解析 execution_settings JSON；空/非法返回默认值.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <returns>执行参数.</returns>
    public static AppAgentExecutionSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppAgentExecutionSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppAgentExecutionSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new AppAgentExecutionSettings();
        }
        catch (JsonException)
        {
            return new AppAgentExecutionSettings();
        }
    }
}
