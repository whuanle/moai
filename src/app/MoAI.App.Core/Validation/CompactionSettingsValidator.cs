using System.Text.Json;
using MoAI.AI;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Validation;

/// <summary>
/// 应用执行参数（execution_settings）中上下文压缩配置的校验：
/// compactionMode 必须是 auto/custom；custom 模式下的绝对值阈值限幅与大小关系校验.
/// </summary>
public static class CompactionSettingsValidator
{
    /// <summary>
    /// 校验执行参数中的上下文压缩配置；未携带压缩相关字段时直接放行.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON 对象.</param>
    public static void Validate(JsonElement executionSettings)
    {
        if (executionSettings.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // 模式标记：显式携带时必须是 auto/custom（缺省按 auto 处理，无需显式携带）
        if (executionSettings.TryGetProperty("compactionMode", out var mode))
        {
            if (mode.ValueKind != JsonValueKind.String
                || (mode.GetString() != AppCompactionPolicy.ModeAuto && mode.GetString() != AppCompactionPolicy.ModeCustom))
            {
                throw new BusinessException($"压缩策略必须是 {AppCompactionPolicy.ModeAuto} 或 {AppCompactionPolicy.ModeCustom}.") { StatusCode = 400 };
            }
        }

        var isCustom = mode.ValueKind == JsonValueKind.String && mode.GetString() == AppCompactionPolicy.ModeCustom;
        if (!isCustom)
        {
            // auto 模式阈值由运行时按模型窗口推导，绝对值字段不生效、不做校验（历史存量值保持兼容）
            return;
        }

        if (TryGetInt(executionSettings, "toolResultTriggerMessages", out var toolResultTrigger)
            && (toolResultTrigger < AppCompactionPolicy.MinToolResultTriggerMessages || toolResultTrigger > AppCompactionPolicy.MaxTokenLimit))
        {
            throw new BusinessException($"工具结果折叠阈值必须是 {AppCompactionPolicy.MinToolResultTriggerMessages}~{AppCompactionPolicy.MaxTokenLimit} 之间的整数（消息数）.") { StatusCode = 400 };
        }

        var hasSummarizeTrigger = TryGetInt(executionSettings, "summarizeTriggerTokens", out var summarizeTrigger);
        if (hasSummarizeTrigger
            && (summarizeTrigger < AppCompactionPolicy.MinTokenLimit || summarizeTrigger > AppCompactionPolicy.MaxTokenLimit))
        {
            throw new BusinessException($"摘要触发 token 必须是 {AppCompactionPolicy.MinTokenLimit}~{AppCompactionPolicy.MaxTokenLimit} 之间的整数.") { StatusCode = 400 };
        }

        var hasHardLimit = TryGetInt(executionSettings, "hardTokenLimit", out var hardLimit);
        if (hasHardLimit
            && (hardLimit < AppCompactionPolicy.MinTokenLimit || hardLimit > AppCompactionPolicy.MaxTokenLimit))
        {
            throw new BusinessException($"紧急截断 token 上限必须是 {AppCompactionPolicy.MinTokenLimit}~{AppCompactionPolicy.MaxTokenLimit} 之间的整数.") { StatusCode = 400 };
        }

        if (hasSummarizeTrigger && hasHardLimit && hardLimit <= summarizeTrigger)
        {
            throw new BusinessException("紧急截断 token 上限必须大于摘要触发 token.") { StatusCode = 400 };
        }
    }

    private static bool TryGetInt(JsonElement executionSettings, string name, out int value)
    {
        value = 0;
        return executionSettings.TryGetProperty(name, out var node)
            && node.ValueKind == JsonValueKind.Number
            && node.TryGetInt32(out value);
    }
}
