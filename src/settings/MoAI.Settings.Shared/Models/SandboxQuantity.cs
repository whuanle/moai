using System.Globalization;
using System.Text.RegularExpressions;

namespace MoAI.Settings.Models;

/// <summary>
/// K8s 数量格式解析（沙箱 CPU / 内存限制），CPU 解析为毫核（1 核 = 1000m），内存解析为字节.
/// </summary>
public static partial class SandboxQuantity
{
    [GeneratedRegex(@"^(\d+(?:\.\d+)?)(m)?$")]
    private static partial Regex CpuRegex();

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)(Ki|Mi|Gi|Ti|Pi|Ei|K|M|G|T|P|E|B)?$")]
    private static partial Regex MemoryRegex();

    /// <summary>
    /// 解析 CPU 数量为毫核：核数（如 "1"、"0.5"）或毫核（如 "500m"）；非法返回 false.
    /// </summary>
    /// <param name="value">CPU 数量文本.</param>
    /// <param name="millicores">换算后的毫核数.</param>
    /// <returns>格式合法返回 true.</returns>
    public static bool TryParseCpu(string? value, out long millicores)
    {
        millicores = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = CpuRegex().Match(value.Trim());
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        var result = amount * 1000m;
        if (match.Groups[2].Value == "m")
        {
            result = amount;
        }

        if (result < 0 || result > long.MaxValue)
        {
            return false;
        }

        millicores = (long)Math.Round(result, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>
    /// 解析内存数量为字节：二进制后缀 Ki/Mi/Gi/Ti/Pi/Ei（1024 进制）、十进制后缀 K/M/G/T/P/E（1000 进制），
    /// 无后缀视为字节；非法返回 false（后缀区分大小写）.
    /// </summary>
    /// <param name="value">内存数量文本.</param>
    /// <param name="bytes">换算后的字节数.</param>
    /// <returns>格式合法返回 true.</returns>
    public static bool TryParseMemory(string? value, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = MemoryRegex().Match(value.Trim());
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        var multiplier = match.Groups[2].Value switch
        {
            "" or "B" => 1m,
            "Ki" => 1024m,
            "Mi" => 1024m * 1024,
            "Gi" => 1024m * 1024 * 1024,
            "Ti" => 1024m * 1024 * 1024 * 1024,
            "Pi" => 1024m * 1024 * 1024 * 1024 * 1024,
            "Ei" => 1024m * 1024 * 1024 * 1024 * 1024 * 1024,
            "K" => 1_000m,
            "M" => 1_000_000m,
            "G" => 1_000_000_000m,
            "T" => 1_000_000_000_000m,
            "P" => 1_000_000_000_000_000m,
            "E" => 1_000_000_000_000_000_000m,
            _ => 0m
        };

        if (multiplier <= 0)
        {
            return false;
        }

        var result = amount * multiplier;
        if (result < 0 || result > long.MaxValue)
        {
            return false;
        }

        bytes = (long)Math.Round(result, MidpointRounding.AwayFromZero);
        return true;
    }
}
