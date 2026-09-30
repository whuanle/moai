using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// SSH 命令白名单守卫（处置类插件的「写操作护栏」）：命令（含管道的每一段）必须命中实例配置的白名单前缀才放行，
/// 拼接/替换符号一律拒绝，灾难级命令任何白名单都不可放行。校验先于建立 SSH 连接.
/// </summary>
/// <remarks>
/// 守卫规则（按序）：
/// <list type="number">
/// <item><description>命令非空、白名单非空（未配置白名单 = 拒绝一切命令，fail-closed）。</description></item>
/// <item><description>拼接与替换符黑名单：<c>; &amp;&amp; || &amp; 反引号 $( 换行</c> —— 这些符号可把任意命令拼到白名单命令之后执行。</description></item>
/// <item><description>管道 <c>|</c> 放行但**每一段**分别校验：先拆段，再逐段命中白名单前缀。</description></item>
/// <item><description>灾难级黑名单（首 token）：mkfs/dd/shutdown/reboot/halt/poweroff/fdisk/parted/init——实例白名单不可放行。</description></item>
/// <item><description>白名单为「命令前缀」语义：段等于条目，或以「条目+空格」开头（<c>df</c> 命中 <c>df -h</c> 但不命中 <c>dfx</c>）。</description></item>
/// </list>
/// </remarks>
internal static class SshCommandGuard
{
    /// <summary>
    /// 拼接与替换符号（出现即拒绝）.
    /// </summary>
    private static readonly string[] ForbiddenSequences = { ";", "&&", "||", "&", "`", "$(", "\n", "\r" };

    /// <summary>
    /// 灾难级命令黑名单（首 token 精确匹配，白名单不可放行）.
    /// </summary>
    private static readonly HashSet<string> ForbiddenCommands = new(StringComparer.Ordinal)
    {
        "mkfs", "dd", "shutdown", "reboot", "halt", "poweroff", "fdisk", "parted", "init",
    };

    /// <summary>
    /// 校验命令是否可执行.
    /// </summary>
    /// <param name="command">待执行命令.</param>
    /// <param name="whitelist">实例配置的命令白名单（逗号分隔前缀）.</param>
    /// <returns>拒绝理由；放行返回 null.</returns>
    public static string? Validate(string? command, string whitelist)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "命令 Command 不能为空";
        }

        if (string.IsNullOrWhiteSpace(whitelist))
        {
            return "实例配置的命令白名单 CommandWhitelist 为空：为防止误操作，未配置白名单时拒绝执行任何命令";
        }

        foreach (var sequence in ForbiddenSequences)
        {
            if (command.Contains(sequence, StringComparison.Ordinal))
            {
                return $"命令包含拼接/替换符号（; && || & 反引号 $( 换行 均不允许），已拒绝以防止绕过白名单";
            }
        }

        foreach (var rawSegment in command.Split('|'))
        {
            var segment = rawSegment.Trim();
            if (segment.Length == 0)
            {
                return "管道段为空，已拒绝";
            }

            var forbidden = MatchForbiddenCommand(segment);
            if (forbidden != null)
            {
                return $"命令段「{segment}」命中灾难级黑名单 {forbidden}，白名单亦不可放行";
            }

            var rmTarget = MatchCatastrophicRm(segment);
            if (rmTarget != null)
            {
                return $"命令段「{segment}」为灾难级递归删除（{rmTarget}），白名单亦不可放行";
            }

            if (!MatchWhitelist(segment, whitelist))
            {
                return $"命令段「{segment}」未命中实例白名单（CommandWhitelist），已拒绝";
            }
        }

        return null;
    }

    /// <summary>
    /// 判定段首 token 是否命中灾难级黑名单.
    /// </summary>
    /// <param name="segment">命令段.</param>
    /// <returns>命中的黑名单名；未命中返回 null.</returns>
    private static string? MatchForbiddenCommand(string segment)
    {
        var firstToken = segment.Split(' ')[0];
        if (ForbiddenCommands.Contains(firstToken))
        {
            return firstToken;
        }

        if (firstToken.StartsWith("mkfs", StringComparison.Ordinal))
        {
            return "mkfs*";
        }

        return null;
    }

    /// <summary>
    /// 判定是否为灾难级递归删除（rm 带递归标志且目标是根/根通配）——即使实例白名单放行了 rm 也拒绝.
    /// </summary>
    /// <param name="segment">命令段.</param>
    /// <returns>命中的目标；未命中返回 null.</returns>
    private static string? MatchCatastrophicRm(string segment)
    {
        var tokens = segment.Split(' ');
        if (tokens[0] != "rm")
        {
            return null;
        }

        var recursive = false;
        foreach (var token in tokens)
        {
            if (token is "-r" or "-R" or "-rf" or "-fr" or "-rF" or "-fR" or "--recursive")
            {
                recursive = true;
            }
        }

        if (!recursive)
        {
            return null;
        }

        foreach (var token in tokens)
        {
            if (token == "/" || token.StartsWith("/*", StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// 段是否命中白名单任一前缀.
    /// </summary>
    private static bool MatchWhitelist(string segment, string whitelist)
    {
        foreach (var rawEntry in whitelist.Split(','))
        {
            var entry = rawEntry.Trim();
            if (entry.Length == 0)
            {
                continue;
            }

            if (segment == entry || segment.StartsWith(entry + " ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
