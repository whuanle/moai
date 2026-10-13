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
    public int ToolResultTriggerMessages { get; set; } = 16;

    /// <summary>
    /// 摘要压缩触发：token 超过该值时对旧对话做摘要.
    /// </summary>
    public int SummarizeTriggerTokens { get; set; } = 16000;

    /// <summary>
    /// 滑动窗口保留的用户轮数.
    /// </summary>
    public int PreserveTurns { get; set; } = 8;

    /// <summary>
    /// 紧急截断的 token 上限.
    /// </summary>
    public int HardTokenLimit { get; set; } = 64000;

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
    /// 子代理协作配置；为空表示未开启（主代理不能创建子代理）.
    /// </summary>
    public SubAgentSettings? SubAgent { get; set; }

    /// <summary>
    /// 长期记忆配置；为空表示未开启.
    /// </summary>
    public AgentMemorySettings? Memory { get; set; }

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

/// <summary>
/// 子代理协作配置：主代理可创建自身同配置的子代理，在独立上下文中执行委派任务，仅结论回传主上下文.
/// </summary>
public sealed class SubAgentSettings
{
    /// <summary>
    /// 默认任务超时（秒）.
    /// </summary>
    public const int DefaultTimeoutSeconds = 180;

    /// <summary>
    /// 默认结果回传字符上限.
    /// </summary>
    public const int DefaultResultMaxChars = 8000;

    /// <summary>
    /// 是否允许主代理创建子代理.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 单次子任务超时（秒），超时后子任务取消并在结果中说明.
    /// </summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>
    /// 子任务结果回传主上下文的字符上限，超长截断.
    /// </summary>
    public int ResultMaxChars { get; set; } = DefaultResultMaxChars;
}
