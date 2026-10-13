using System.Text.Json;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Validation;

/// <summary>
/// 应用执行参数（execution_settings）中子代理协作配置的校验：
/// 取值范围限幅；外部应用不允许开启（外部用户/匿名会话不可创建子代理消耗资源）.
/// </summary>
public static class SubAgentSettingsValidator
{
    /// <summary>
    /// 单次子任务超时下限（秒）.
    /// </summary>
    public const int MinTimeoutSeconds = 30;

    /// <summary>
    /// 单次子任务超时上限（秒）.
    /// </summary>
    public const int MaxTimeoutSeconds = 1800;

    /// <summary>
    /// 结果回传字符上限下限.
    /// </summary>
    public const int MinResultMaxChars = 500;

    /// <summary>
    /// 结果回传字符上限上限.
    /// </summary>
    public const int MaxResultMaxChars = 100000;

    /// <summary>
    /// 判断本次保存是否开启子代理协作（subAgent.enabled=true）；未携带或未启用返回 false.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON.</param>
    /// <returns>是否开启子代理协作.</returns>
    public static bool IsSubAgentEnabled(JsonElement? executionSettings)
    {
        if (executionSettings is null || executionSettings.Value.ValueKind != JsonValueKind.Object
            || !executionSettings.Value.TryGetProperty("subAgent", out var subAgent)
            || subAgent.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return subAgent.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// 校验执行参数中的子代理配置；未携带 subAgent 节时直接放行.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON 对象.</param>
    public static void Validate(JsonElement executionSettings)
    {
        if (executionSettings.ValueKind != JsonValueKind.Object
            || !executionSettings.TryGetProperty("subAgent", out var subAgent)
            || subAgent.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (subAgent.TryGetProperty("timeoutSeconds", out var timeout) && timeout.ValueKind == JsonValueKind.Number)
        {
            if (!timeout.TryGetInt32(out var seconds) || seconds < MinTimeoutSeconds || seconds > MaxTimeoutSeconds)
            {
                throw new BusinessException($"子任务超时必须是 {MinTimeoutSeconds}~{MaxTimeoutSeconds} 之间的整数（秒）.") { StatusCode = 400 };
            }
        }

        if (subAgent.TryGetProperty("resultMaxChars", out var maxChars) && maxChars.ValueKind == JsonValueKind.Number)
        {
            if (!maxChars.TryGetInt32(out var chars) || chars < MinResultMaxChars || chars > MaxResultMaxChars)
            {
                throw new BusinessException($"子任务结果回传上限必须是 {MinResultMaxChars}~{MaxResultMaxChars} 之间的整数（字符）.") { StatusCode = 400 };
            }
        }
    }
}
