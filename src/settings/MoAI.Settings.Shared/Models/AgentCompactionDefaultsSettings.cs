using System.Text.Json;

namespace MoAI.Settings.Models;

/// <summary>
/// Agent 上下文压缩默认参数（系统级默认，由超级管理员在系统设置页调整；应用可在 execution_settings 中按字段覆盖）.
/// </summary>
public class AgentCompactionDefaultsSettings
{
    /// <summary>
    /// 工具结果压缩触发：消息数超过该值时折叠旧工具结果，范围 1~500.
    /// </summary>
    public int ToolResultTriggerMessages { get; set; } = 12;

    /// <summary>
    /// 摘要压缩触发：token 超过该值时对旧对话做摘要，范围 200~2000000.
    /// </summary>
    public int SummarizeTriggerTokens { get; set; } = 4000;

    /// <summary>
    /// 滑动窗口保留的用户轮数，范围 1~10000.
    /// </summary>
    public int PreserveTurns { get; set; } = 6;

    /// <summary>
    /// 紧急截断的 token 上限，范围 500~4000000.
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
    /// 工具结果折叠触发消息数下限.
    /// </summary>
    public const int MinToolResultTriggerMessages = 1;

    /// <summary>
    /// 工具结果折叠触发消息数上限.
    /// </summary>
    public const int MaxToolResultTriggerMessages = 500;

    /// <summary>
    /// 摘要触发 token 下限.
    /// </summary>
    public const int MinSummarizeTriggerTokens = 200;

    /// <summary>
    /// 摘要触发 token 上限.
    /// </summary>
    public const int MaxSummarizeTriggerTokens = 2000000;

    /// <summary>
    /// 滑动窗口保留轮数下限.
    /// </summary>
    public const int MinPreserveTurns = 1;

    /// <summary>
    /// 滑动窗口保留轮数上限.
    /// </summary>
    public const int MaxPreserveTurns = 10000;

    /// <summary>
    /// 紧急截断 token 下限.
    /// </summary>
    public const int MinHardTokenLimit = 500;

    /// <summary>
    /// 紧急截断 token 上限.
    /// </summary>
    public const int MaxHardTokenLimit = 4000000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 解析系统设置存储的 JSON；空或非法时返回内置默认值，越界字段钳制到合法范围.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <returns>压缩默认参数.</returns>
    public static AgentCompactionDefaultsSettings Parse(string? json)
    {
        var result = new AgentCompactionDefaultsSettings();
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AgentCompactionDefaultsSettings>(json, JsonOptions);
            if (parsed == null)
            {
                return result;
            }

            parsed.ToolResultTriggerMessages = Math.Clamp(parsed.ToolResultTriggerMessages, MinToolResultTriggerMessages, MaxToolResultTriggerMessages);
            parsed.SummarizeTriggerTokens = Math.Clamp(parsed.SummarizeTriggerTokens, MinSummarizeTriggerTokens, MaxSummarizeTriggerTokens);
            parsed.PreserveTurns = Math.Clamp(parsed.PreserveTurns, MinPreserveTurns, MaxPreserveTurns);
            parsed.HardTokenLimit = Math.Clamp(parsed.HardTokenLimit, MinHardTokenLimit, MaxHardTokenLimit);
            return parsed;
        }
        catch (JsonException)
        {
            return result;
        }
    }

    /// <summary>
    /// 校验待保存的 JSON 值：必须是对象、字段类型正确且在合法范围内.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <param name="error">校验失败原因.</param>
    /// <returns>是否通过校验.</returns>
    public static bool TryValidateJson(string? json, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "压缩默认参数不能为空.";
            return false;
        }

        AgentCompactionDefaultsSettings? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AgentCompactionDefaultsSettings>(json, JsonOptions);
        }
        catch (JsonException)
        {
            error = "压缩默认参数必须是合法 JSON 对象.";
            return false;
        }

        if (parsed == null)
        {
            error = "压缩默认参数必须是合法 JSON 对象.";
            return false;
        }

        if (parsed.ToolResultTriggerMessages < MinToolResultTriggerMessages || parsed.ToolResultTriggerMessages > MaxToolResultTriggerMessages)
        {
            error = $"工具结果折叠触发消息数必须是 {MinToolResultTriggerMessages}~{MaxToolResultTriggerMessages} 之间的整数.";
            return false;
        }

        if (parsed.SummarizeTriggerTokens < MinSummarizeTriggerTokens || parsed.SummarizeTriggerTokens > MaxSummarizeTriggerTokens)
        {
            error = $"摘要触发 token 必须是 {MinSummarizeTriggerTokens}~{MaxSummarizeTriggerTokens} 之间的整数.";
            return false;
        }

        if (parsed.PreserveTurns < MinPreserveTurns || parsed.PreserveTurns > MaxPreserveTurns)
        {
            error = $"滑动窗口保留轮数必须是 {MinPreserveTurns}~{MaxPreserveTurns} 之间的整数.";
            return false;
        }

        if (parsed.HardTokenLimit < MinHardTokenLimit || parsed.HardTokenLimit > MaxHardTokenLimit)
        {
            error = $"紧急截断 token 上限必须是 {MinHardTokenLimit}~{MaxHardTokenLimit} 之间的整数.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(parsed.SummarizerModelId) && !Guid.TryParse(parsed.SummarizerModelId, out _))
        {
            error = "摘要模型 id 必须是合法的应用模型 id 或留空.";
            return false;
        }

        return true;
    }
}
