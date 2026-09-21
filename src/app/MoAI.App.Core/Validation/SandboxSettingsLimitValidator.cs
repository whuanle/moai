using System.Text.Json;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Models;

namespace MoAI.App.Validation;

/// <summary>
/// 应用执行参数（execution_settings）中沙箱配置的上限校验：启用沙箱时，存活时间 / CPU / 内存不得超出系统设置的全局上限.
/// </summary>
public static class SandboxSettingsLimitValidator
{
    /// <summary>
    /// 判断本次保存是否启用沙箱（sandbox.enabled=true）；未携带执行参数或未启用返回 false.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON.</param>
    /// <returns>本次保存是否启用沙箱.</returns>
    public static bool IsSandboxEnabled(JsonElement? executionSettings)
    {
        if (executionSettings is null || executionSettings.Value.ValueKind != JsonValueKind.Object
            || !executionSettings.Value.TryGetProperty("sandbox", out var sandbox)
            || sandbox.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return sandbox.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// 校验执行参数中的沙箱配置；未携带沙箱或沙箱未启用时直接放行（保留存量配置，避免收紧上限后连其他字段的保存也被阻断）.
    /// </summary>
    /// <param name="executionSettings">执行参数 JSON 对象.</param>
    /// <param name="limits">系统沙箱上限.</param>
    public static void Validate(JsonElement executionSettings, SandboxLimitsSettings limits)
    {
        if (executionSettings.ValueKind != JsonValueKind.Object
            || !executionSettings.TryGetProperty("sandbox", out var sandbox)
            || sandbox.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // 仅在本次保存启用沙箱时校验取值；未启用仅暂存配置，运行时不会生效
        if (!sandbox.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.True)
        {
            return;
        }

        if (sandbox.TryGetProperty("timeoutSeconds", out var timeout)
            && timeout.ValueKind == JsonValueKind.Number
            && timeout.TryGetInt32(out var seconds)
            && seconds > 0
            && seconds > limits.MaxTtlSeconds)
        {
            throw new BusinessException($"沙箱存活时间不能超过系统上限 {limits.MaxTtlSeconds} 秒.") { StatusCode = 400 };
        }

        if (!sandbox.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (resource.TryGetProperty("cpu", out var cpu) && cpu.ValueKind == JsonValueKind.String)
        {
            var cpuText = cpu.GetString()?.Trim();
            if (!string.IsNullOrEmpty(cpuText))
            {
                if (!SandboxQuantity.TryParseCpu(cpuText, out var millicores))
                {
                    throw new BusinessException("沙箱 CPU 限制格式无效，例如 1 或 500m.") { StatusCode = 400 };
                }

                if (millicores > limits.MaxCpuMillicores)
                {
                    throw new BusinessException($"沙箱 CPU 限制不能超过系统上限 {limits.MaxCpu}.") { StatusCode = 400 };
                }
            }
        }

        if (resource.TryGetProperty("memory", out var memory) && memory.ValueKind == JsonValueKind.String)
        {
            var memoryText = memory.GetString()?.Trim();
            if (!string.IsNullOrEmpty(memoryText))
            {
                if (!SandboxQuantity.TryParseMemory(memoryText, out var bytes))
                {
                    throw new BusinessException("沙箱内存限制格式无效，例如 2Gi 或 512Mi.") { StatusCode = 400 };
                }

                if (bytes > limits.MaxMemoryBytes)
                {
                    throw new BusinessException($"沙箱内存限制不能超过系统上限 {limits.MaxMemory}.") { StatusCode = 400 };
                }
            }
        }
    }
}
