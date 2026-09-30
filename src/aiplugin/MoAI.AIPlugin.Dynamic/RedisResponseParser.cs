using System;
using System.Collections.Generic;
using System.Globalization;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Redis 诊断响应解析（纯函数，便于单测）：INFO 文本分节、CLIENT LIST 行解析、SLOWLOG 条目组装.
/// </summary>
internal static class RedisResponseParser
{
    /// <summary>
    /// 慢查询参数拼接的最大字符数.
    /// </summary>
    private const int MaxArgChars = 256;

    /// <summary>
    /// 解析 INFO 文本为「节段 → 键值」结构.
    /// </summary>
    /// <param name="raw">INFO 原文（行以 \r\n 分隔，节段头形如 "# Server"）.</param>
    /// <returns>节段名（小写）→ 键值字典.</returns>
    public static Dictionary<string, Dictionary<string, string>> ParseInfo(string raw)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<string, string>(StringComparer.Ordinal);
        sections["default"] = current;
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                sections[line[2..].Trim().ToLowerInvariant()] = current;
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            current[line[..separator]] = line[(separator + 1)..];
        }

        return sections;
    }

    /// <summary>
    /// 解析 CLIENT LIST 文本（每行一个客户端，行内空格分隔的 k=v）.
    /// </summary>
    /// <param name="raw">CLIENT LIST 原文.</param>
    /// <returns>每个客户端一个键值字典；value 缺失时以空串占位.</returns>
    public static List<Dictionary<string, string>> ParseClientList(string raw)
    {
        var result = new List<Dictionary<string, string>>();
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var client = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in line.Split(' '))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                client[pair[..separator]] = pair[(separator + 1)..];
            }

            if (client.Count > 0)
            {
                result.Add(client);
            }
        }

        return result;
    }

    /// <summary>
    /// 组装 SLOWLOG GET 条目（结构拆解由插件完成，这里只做格式化）.
    /// </summary>
    /// <param name="id">条目 ID.</param>
    /// <param name="clockSeconds">命令完成时间（Unix 秒）.</param>
    /// <param name="durationMicros">执行耗时（微秒）.</param>
    /// <param name="command">命令名（已大写）.</param>
    /// <param name="args">参数（空格拼接原文）.</param>
    /// <param name="clientAddress">客户端地址（Redis 4+，可空）.</param>
    /// <param name="clientName">客户端名（Redis 4+，可空）.</param>
    /// <returns>慢查询条目.</returns>
    public static Models.RedisSlowLogEntry BuildSlowLogEntry(long id, long clockSeconds, long durationMicros, string command, string args, string clientAddress, string clientName)
    {
        return new Models.RedisSlowLogEntry
        {
            Id = id,
            Timestamp = clockSeconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(clockSeconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                : string.Empty,
            DurationMicros = durationMicros,
            Command = command,
            Args = args.Length > MaxArgChars ? args[..MaxArgChars] + "..." : args,
            ClientAddress = clientAddress,
            ClientName = clientName,
        };
    }

    /// <summary>
    /// 按键类型映射元素个数命令.
    /// </summary>
    /// <param name="type">TYPE 返回的类型（小写）.</param>
    /// <returns>STRLEN/HLEN/LLEN/SCARD/ZCARD/XLEN；不适用（none/module/未知）返回 null.</returns>
    public static string? LengthCommandFor(string type)
    {
        return type switch
        {
            "string" => "STRLEN",
            "hash" => "HLEN",
            "list" => "LLEN",
            "set" => "SCARD",
            "zset" => "ZCARD",
            "stream" => "XLEN",
            _ => null,
        };
    }
}
