using System.Net;
using System.Text.RegularExpressions;

namespace MoAI.Settings.Models;

/// <summary>
/// 沙箱出站网络策略常量与目标格式解析（域名 / 通配符域名 / IP / CIDR），供应用配置校验、系统设置校验与运行时策略下发共用.
/// <para>目标格式与 OpenSandbox egress 服务端对齐：域名目标不支持端口限制（DNS 层无端口维度），IP / CIDR 目标可限定端口.</para>
/// </summary>
public static partial class SandboxNetworkTarget
{
    /// <summary>
    /// 单个沙箱策略的规则数上限.
    /// </summary>
    public const int MaxRulesPerPolicy = 100;

    /// <summary>
    /// 单条规则的端口数上限（服务端允许 256，平台侧收紧）.
    /// </summary>
    public const int MaxPortsPerRule = 16;

    /// <summary>
    /// 域名格式：可选 <c>*.</c> 通配符前缀 + 至少两级域名.
    /// </summary>
    [GeneratedRegex(@"^(\*\.)?[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$", RegexOptions.IgnoreCase)]
    private static partial Regex DomainRegex();

    /// <summary>
    /// 归一化规则动作：allow / deny（忽略大小写与空白）；非法返回 null.
    /// </summary>
    /// <param name="value">动作原文.</param>
    /// <returns>归一化后的动作，非法为 null.</returns>
    public static string? NormalizeAction(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "allow" => "allow",
            "deny" => "deny",
            _ => null
        };

    /// <summary>
    /// 校验默认动作：allow / deny / 空（留空由服务端语义决定）.
    /// </summary>
    /// <param name="value">默认动作原文.</param>
    /// <returns>是否合法.</returns>
    public static bool IsValidDefaultAction(string? value)
        => string.IsNullOrWhiteSpace(value) || NormalizeAction(value) != null;

    /// <summary>
    /// 校验规则目标：域名（含 <c>*.</c> 通配符）、IPv4 / IPv6 地址或 CIDR 网段.
    /// </summary>
    /// <param name="value">目标原文.</param>
    /// <param name="isIpOrCidr">是否 IP / CIDR 目标（此类目标才允许端口限制）.</param>
    /// <returns>格式合法返回 true.</returns>
    public static bool TryParseTarget(string? value, out bool isIpOrCidr)
    {
        isIpOrCidr = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length > 253)
        {
            return false;
        }

        // CIDR：ip/前缀
        var slashIndex = text.IndexOf('/');
        if (slashIndex >= 0)
        {
            if (text.IndexOf('/', slashIndex + 1) >= 0)
            {
                return false;
            }

            var ipPart = text[..slashIndex];
            var prefixText = text[(slashIndex + 1)..];
            if (!TryParseIp(ipPart, out var isIpv6) || !int.TryParse(prefixText, out var prefix))
            {
                return false;
            }

            var maxPrefix = isIpv6 ? 128 : 32;
            if (prefix < 0 || prefix > maxPrefix)
            {
                return false;
            }

            isIpOrCidr = true;
            return true;
        }

        // 纯 IP（要求包含 . 或 :，避免 IPAddress.TryParse 把 "1" 当作 0.0.0.1）；IP 解析失败再尝试域名（如 pypi.org）
        if (text.Contains('.') || text.Contains(':'))
        {
            if (IPAddress.TryParse(text, out _))
            {
                isIpOrCidr = true;
                return true;
            }

            if (text.Contains(':'))
            {
                return false;
            }
        }

        return DomainRegex().IsMatch(text);
    }

    /// <summary>
    /// 校验单条规则的端口列表：1~65535、去重、数量不超过 <see cref="MaxPortsPerRule"/>.
    /// </summary>
    /// <param name="ports">端口集合.</param>
    /// <returns>全部合法返回 true.</returns>
    public static bool TryParsePorts(System.Collections.Generic.IEnumerable<int>? ports)
    {
        if (ports == null)
        {
            return true;
        }

        var count = 0;
        var seen = new HashSet<int>();
        foreach (var port in ports)
        {
            count++;
            if (count > MaxPortsPerRule || port < 1 || port > 65535 || !seen.Add(port))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 解析逗号分隔的内网网段设置（IP 或 CIDR），任一条非法返回 false.
    /// </summary>
    /// <param name="value">设置原文，逗号分隔.</param>
    /// <param name="cidrs">归一化后的网段列表.</param>
    /// <returns>全部合法且非空返回 true.</returns>
    public static bool TryParseCidrList(string? value, out System.Collections.Generic.IReadOnlyList<string> cidrs)
    {
        cidrs = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var result = new List<string>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseTarget(part, out var isIpOrCidr) || !isIpOrCidr)
            {
                return false;
            }

            result.Add(part);
        }

        if (result.Count == 0)
        {
            return false;
        }

        cidrs = result;
        return true;
    }

    private static bool TryParseIp(string value, out bool isIpv6)
    {
        isIpv6 = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        isIpv6 = value.Contains(':');
        if (!isIpv6 && !value.Contains('.'))
        {
            return false;
        }

        return IPAddress.TryParse(value, out _);
    }
}
