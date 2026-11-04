using System.Text.Json;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Models;

namespace MoAI.App.Validation;

/// <summary>
/// 应用执行参数（execution_settings）中沙箱配置的上限校验：启用沙箱时，存活时间 / CPU / 内存不得超出系统设置的全局上限；
/// 出站网络策略（network）的目标格式 / 端口约束 / 规则数量与平台管控模式一并强校验.
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

        ValidateNetwork(sandbox, limits);
        ValidateCommandFilter(sandbox);

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

    /// <summary>
    /// 校验出站网络策略：<c>sandbox.network</c> 的默认动作、规则目标（域名 / IP / CIDR）与端口约束，
    /// 以及平台管控模式（deny / intranet 模式下不允许应用声明「默认允许」）.
    /// </summary>
    /// <param name="sandbox">sandbox 配置 JSON 对象.</param>
    /// <param name="limits">系统沙箱上限（含网络管控模式）.</param>
    private static void ValidateNetwork(JsonElement sandbox, SandboxLimitsSettings limits)
    {
        if (!sandbox.TryGetProperty("network", out var network) || network.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string? defaultAction = null;
        if (network.TryGetProperty("defaultAction", out var defaultActionElement))
        {
            if (defaultActionElement.ValueKind != JsonValueKind.String)
            {
                throw new BusinessException("沙箱出站网络默认动作无效，可选 allow / deny.") { StatusCode = 400 };
            }

            defaultAction = defaultActionElement.GetString()?.Trim().ToLowerInvariant();
            if (!SandboxNetworkTarget.IsValidDefaultAction(defaultAction))
            {
                throw new BusinessException("沙箱出站网络默认动作无效，可选 allow / deny.") { StatusCode = 400 };
            }
        }

        var ruleCount = 0;

        // 新格式 rules：每条 { action, target, ports? }
        if (network.TryGetProperty("rules", out var rules))
        {
            if (rules.ValueKind != JsonValueKind.Array)
            {
                throw new BusinessException("沙箱出站规则必须为数组.") { StatusCode = 400 };
            }

            foreach (var rule in rules.EnumerateArray())
            {
                ruleCount++;
                if (ruleCount > SandboxNetworkTarget.MaxRulesPerPolicy)
                {
                    throw new BusinessException($"沙箱出站规则不能超过 {SandboxNetworkTarget.MaxRulesPerPolicy} 条.") { StatusCode = 400 };
                }

                if (rule.ValueKind != JsonValueKind.Object)
                {
                    throw new BusinessException("沙箱出站规则必须为对象（action / target / ports）.") { StatusCode = 400 };
                }

                string? action = null;
                if (rule.TryGetProperty("action", out var actionElement))
                {
                    if (actionElement.ValueKind != JsonValueKind.String
                        || (action = SandboxNetworkTarget.NormalizeAction(actionElement.GetString())) == null)
                    {
                        throw new BusinessException("沙箱出站规则动作无效，可选 allow / deny.") { StatusCode = 400 };
                    }
                }

                var hasTarget = rule.TryGetProperty("target", out var targetElement) && targetElement.ValueKind == JsonValueKind.String;
                var hasPorts = rule.TryGetProperty("ports", out var portsElement) && portsElement.ValueKind == JsonValueKind.Array;
                if (!hasTarget && !hasPorts)
                {
                    throw new BusinessException("沙箱出站规则必须填写目标或端口.") { StatusCode = 400 };
                }

                var isIpOrCidr = false;
                if (hasTarget)
                {
                    var target = targetElement.GetString();
                    if (!SandboxNetworkTarget.TryParseTarget(target, out isIpOrCidr))
                    {
                        throw new BusinessException($"沙箱出站规则目标格式无效：{target}（支持域名、*.通配符、IP 或 CIDR 网段）.") { StatusCode = 400 };
                    }
                }

                if (hasPorts)
                {
                    var ports = new List<int>();
                    foreach (var portElement in portsElement.EnumerateArray())
                    {
                        if (portElement.ValueKind != JsonValueKind.Number || !portElement.TryGetInt32(out var port))
                        {
                            throw new BusinessException("沙箱出站规则端口必须为整数（1~65535）.") { StatusCode = 400 };
                        }

                        ports.Add(port);
                    }

                    if (!SandboxNetworkTarget.TryParsePorts(ports))
                    {
                        throw new BusinessException($"沙箱出站规则端口无效：须为 1~65535 的不重复整数且单条不超过 {SandboxNetworkTarget.MaxPortsPerRule} 个.") { StatusCode = 400 };
                    }

                    // 与服务端对齐：域名目标在 DNS 层评估，不支持端口限制
                    if (hasTarget && !isIpOrCidr)
                    {
                        throw new BusinessException("沙箱出站规则的端口限制仅支持 IP / CIDR 目标（域名目标不支持端口）.") { StatusCode = 400 };
                    }

                    // 纯端口规则（无目标）只在「默认允许」下有黑名单意义，默认动作缺失时无意义
                    if (!hasTarget && action == "allow" && defaultAction != "allow")
                    {
                        throw new BusinessException("仅端口的放行规则要求默认动作为 allow.") { StatusCode = 400 };
                    }
                }
            }
        }

        // 旧格式 egress：字符串数组，按目标格式校验
        if (network.TryGetProperty("egress", out var egress))
        {
            if (egress.ValueKind != JsonValueKind.Array)
            {
                throw new BusinessException("沙箱出站例外列表必须为数组.") { StatusCode = 400 };
            }

            foreach (var entry in egress.EnumerateArray())
            {
                ruleCount++;
                if (ruleCount > SandboxNetworkTarget.MaxRulesPerPolicy)
                {
                    throw new BusinessException($"沙箱出站规则不能超过 {SandboxNetworkTarget.MaxRulesPerPolicy} 条.") { StatusCode = 400 };
                }

                if (entry.ValueKind != JsonValueKind.String
                    || !SandboxNetworkTarget.TryParseTarget(entry.GetString(), out _))
                {
                    throw new BusinessException($"沙箱出站例外目标格式无效：{entry.GetString()}（支持域名、*.通配符、IP 或 CIDR 网段）.") { StatusCode = 400 };
                }
            }
        }

        // 平台管控：deny / intranet 模式下不允许应用声明「默认允许」
        if (defaultAction == "allow"
            && (limits.NetworkMode == NetworkModes.Deny || limits.NetworkMode == NetworkModes.Intranet))
        {
            throw new BusinessException("当前平台已强制沙箱默认拒绝出站，应用不能配置默认允许（可改为追加放行规则）.") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 校验应用级命令过滤配置：<c>sandbox.commandFilter</c> 的 deny 清单条数 / 长度 / regex: 可编译性.
    /// </summary>
    /// <param name="sandbox">sandbox 配置 JSON 对象.</param>
    private static void ValidateCommandFilter(JsonElement sandbox)
    {
        if (!sandbox.TryGetProperty("commandFilter", out var commandFilter) || commandFilter.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (commandFilter.TryGetProperty("enabled", out var enabled) && enabled.ValueKind != JsonValueKind.True && enabled.ValueKind != JsonValueKind.False)
        {
            throw new BusinessException("沙箱命令过滤开关必须为布尔值.") { StatusCode = 400 };
        }

        if (!commandFilter.TryGetProperty("deny", out var deny) || deny.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (deny.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException("沙箱命令过滤黑名单必须为数组.") { StatusCode = 400 };
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in deny.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
            {
                throw new BusinessException("沙箱命令过滤黑名单条目必须为字符串.") { StatusCode = 400 };
            }

            var pattern = entry.GetString()?.Trim() ?? string.Empty;
            if (pattern.Length == 0)
            {
                continue;
            }

            if (!seen.Add(pattern))
            {
                continue;
            }

            if (seen.Count > SandboxCommandPatterns.MaxPatterns)
            {
                throw new BusinessException($"沙箱命令过滤黑名单不能超过 {SandboxCommandPatterns.MaxPatterns} 条.") { StatusCode = 400 };
            }

            if (!SandboxCommandPatterns.IsValidPattern(pattern))
            {
                throw new BusinessException($"沙箱命令过滤模式无效：{pattern}（regex: 前缀须为可编译正则，单条不超过 {SandboxCommandPatterns.MaxPatternLength} 字符）.") { StatusCode = 400 };
            }
        }
    }
}
