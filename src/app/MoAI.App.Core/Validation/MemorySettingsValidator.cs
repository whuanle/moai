using System.Text.Json;
using MoAI.AI.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Validation;

/// <summary>
/// 应用执行参数（execution_settings）中长期记忆配置的形状与取值范围校验；
/// 向量化模型/维度统一由系统设置（MEMORY_*）决定，应用配置不接收这两个字段，
/// 平台总开关门禁需要数据库访问，由保存 Handler 在 R3 管线中补充执行.
/// </summary>
public static class MemorySettingsValidator
{
    /// <summary>
    /// 检索条数上限.
    /// </summary>
    public const int MaxRetrievalTopK = 50;

    /// <summary>
    /// 单用户记忆条数上限.
    /// </summary>
    public const int MaxPerUser = 10000;

    /// <summary>
    /// 判断本次保存是否开启长期记忆（memory.enabled=true）；未携带或未启用返回 false.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON.</param>
    /// <returns>是否开启长期记忆.</returns>
    public static bool IsMemoryEnabled(JsonElement? executionSettings)
    {
        if (executionSettings is null || executionSettings.Value.ValueKind != JsonValueKind.Object
            || !executionSettings.Value.TryGetProperty("memory", out var memory)
            || memory.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return memory.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// 校验执行参数中的记忆配置；未携带 memory 节时直接放行.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON 对象.</param>
    public static void Validate(JsonElement executionSettings)
    {
        if (executionSettings.ValueKind != JsonValueKind.Object
            || !executionSettings.TryGetProperty("memory", out var memory)
            || memory.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (memory.TryGetProperty("retrievalTopK", out var topK) && topK.ValueKind == JsonValueKind.Number)
        {
            if (!topK.TryGetInt32(out var value) || value < 1 || value > MaxRetrievalTopK)
            {
                throw new BusinessException($"记忆检索条数必须是 1~{MaxRetrievalTopK} 之间的整数.") { StatusCode = 400 };
            }
        }

        if (memory.TryGetProperty("minScore", out var minScore) && minScore.ValueKind == JsonValueKind.Number)
        {
            if (!minScore.TryGetDouble(out var value) || value < 0 || value > 1)
            {
                throw new BusinessException("记忆相似度阈值必须是 0~1 之间的数字.") { StatusCode = 400 };
            }
        }

        if (memory.TryGetProperty("maxPerUser", out var maxPerUser) && maxPerUser.ValueKind == JsonValueKind.Number)
        {
            if (!maxPerUser.TryGetInt32(out var value) || value < 10 || value > MaxPerUser)
            {
                throw new BusinessException($"单用户记忆条数上限必须是 10~{MaxPerUser} 之间的整数.") { StatusCode = 400 };
            }
        }

        if (memory.TryGetProperty("prompt", out var prompt) && prompt.ValueKind == JsonValueKind.String)
        {
            var text = prompt.GetString();
            if (!string.IsNullOrEmpty(text) && text.Length > AgentMemorySettings.MaxPromptLength)
            {
                throw new BusinessException($"记忆提示词长度不能超过 {AgentMemorySettings.MaxPromptLength} 字.") { StatusCode = 400 };
            }
        }

        if (memory.TryGetProperty("allowGlobalMemory", out var allowGlobal) && allowGlobal.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new BusinessException("允许用户接入全局记忆必须为布尔值.") { StatusCode = 400 };
        }
    }
}
