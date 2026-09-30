using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Redis 只读诊断（动态插件）：让 AI 读取 Redis 运行信息/慢查询/客户端/配置/单键体检做缓存故障定位.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="RedisQueryRequest.Mode"/>）：info / dbsize / slowlog / client_list / config_get / key_info。
/// 只读保证：插件不接收自由命令，仅按模式下发白名单内的诊断命令（INFO/DBSIZE/SLOWLOG GET/CLIENT LIST/CONFIG GET/
/// TYPE/TTL/STRLEN/HLEN/LLEN/SCARD/ZCARD/XLEN/MEMORY USAGE）；传输层用 <see cref="RedisRespClient"/>，
/// 每次运行新建连接（AUTH/SELECT 仅在有配置时下发）用完即弃。每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件.
/// </remarks>
[AiPlugin(
    key: "redis_query",
    Name = "Redis 只读诊断",
    Description = "Redis 只读诊断：Mode 支持 info（运行信息，如 memory/keyspace）/dbsize（键数量）/slowlog（慢查询）/client_list（客户端连接）/config_get（读配置）/key_info（单键类型/TTL/长度/内存占用）；先以 {\"Mode\":\"info\",\"Section\":\"memory\"} 起步")]
public class RedisQueryPlugin : IDynamicPluginRuntime<RedisQueryRequest, RedisQueryResponse, RedisQueryConfig>
{
    /// <summary>超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 120;

    /// <summary>列表条数上限的下界.</summary>
    private const int MinMaxListItems = 1;

    /// <summary>列表条数上限的上界.</summary>
    private const int MaxMaxListItems = 500;

    /// <summary>慢查询取回条数的上界.</summary>
    private const int MaxSlowLogCount = 128;

    /// <summary>逻辑库编号的上界（实例配置按 0-15 归一）.</summary>
    private const int MaxDatabase = 15;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "info", "dbsize", "slowlog", "client_list", "config_get", "key_info",
    };

    private RedisQueryConfig _config = new();

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "info",        // info | dbsize | slowlog | client_list | config_get | key_info
              "Section": "memory",   // info 模式：节段（可空=全部），server/clients/memory/persistence/stats/replication/keyspace
              "Count": 25,           // slowlog 模式：最多取回条数 1-128
              "Pattern": "*",        // config_get 模式：配置项通配符，如 maxmemory*
              "Key": "session:1001"  // key_info 模式：目标键名
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "Host": "127.0.0.1",   // Redis 主机
              "Port": 6379,          // 端口 1-65535
              "Username": "",        // 可选，Redis 6+ ACL
              "Password": "",        // 可选
              "Ssl": false,          // 是否 TLS
              "Database": 0,         // 逻辑库 0-15
              "TimeoutSeconds": 10,  // 连接与命令超时秒数，1-120
              "MaxListItems": 100    // 列表最多返回条数，1-500
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(RedisQueryConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            return Task.FromResult<string?>("Redis 主机 Host 不能为空");
        }

        if (config.Port < 1 || config.Port > 65535)
        {
            return Task.FromResult<string?>($"Redis 端口必须在 1-65535，收到 {config.Port}");
        }

        _config = new RedisQueryConfig
        {
            Host = config.Host.Trim(),
            Port = config.Port,
            Username = config.Username.Trim(),
            Password = config.Password,
            Ssl = config.Ssl,
            Database = Math.Clamp(config.Database, 0, MaxDatabase),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxListItems = Math.Clamp(config.MaxListItems, MinMaxListItems, MaxMaxListItems),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<RedisQueryResponse> RunAsync(RedisQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        await using var client = new RedisRespClient(_config.Host, _config.Port, _config.Ssl, _config.TimeoutSeconds);
        try
        {
            await client.ConnectAsync(NullIfEmpty(_config.Username), NullIfEmpty(_config.Password), _config.Database, cancellationToken).ConfigureAwait(false);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            if (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new BusinessException(502, $"Redis 连接失败：连接超时（{_config.TimeoutSeconds}s），请检查主机端口与网络");
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Redis 连接失败：{ex.Message}");
        }

        return mode switch
        {
            "dbsize" => await DbsizeAsync(client, cancellationToken).ConfigureAwait(false),
            "slowlog" => await SlowLogAsync(request, client, cancellationToken).ConfigureAwait(false),
            "client_list" => await ClientListAsync(client, cancellationToken).ConfigureAwait(false),
            "config_get" => await ConfigGetAsync(request, client, cancellationToken).ConfigureAwait(false),
            "key_info" => await KeyInfoAsync(request, client, cancellationToken).ConfigureAwait(false),
            _ => await InfoAsync(request, client, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "info";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 info/dbsize/slowlog/client_list/config_get/key_info");
        }

        return normalized;
    }

    private static string? NullIfEmpty(string value)
    {
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// 宽松读取回复里的整数值（部分字段可能以字符串形式返回数字）.
    /// </summary>
    private static long ToLong(object? value)
    {
        return value switch
        {
            long number => number,
            string text when long.TryParse(text, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static string AsString(object? value)
    {
        return value as string ?? string.Empty;
    }

    private static List<object?>? AsArray(object? value)
    {
        return value as List<object?>;
    }

    private async Task<RedisQueryResponse> InfoAsync(RedisQueryRequest request, RedisRespClient client, CancellationToken cancellationToken)
    {
        var section = (request.Section ?? string.Empty).Trim();
        var raw = AsString(await client.ExecAsync("INFO", section.Length == 0 ? Array.Empty<object>() : new object[] { section }, cancellationToken).ConfigureAwait(false));
        return new RedisQueryResponse { ResultType = "info", Sections = RedisResponseParser.ParseInfo(raw) };
    }

    private async Task<RedisQueryResponse> DbsizeAsync(RedisRespClient client, CancellationToken cancellationToken)
    {
        var size = ToLong(await client.ExecAsync("DBSIZE", Array.Empty<object>(), cancellationToken).ConfigureAwait(false));
        return new RedisQueryResponse { ResultType = "dbsize", DbSize = size };
    }

    private async Task<RedisQueryResponse> SlowLogAsync(RedisQueryRequest request, RedisRespClient client, CancellationToken cancellationToken)
    {
        var count = Math.Clamp(request.Count <= 0 ? 25 : request.Count, 1, MaxSlowLogCount);
        var result = AsArray(await client.ExecAsync("SLOWLOG", new object[] { "GET", count.ToString() }, cancellationToken).ConfigureAwait(false));
        var entries = new List<RedisSlowLogEntry>();
        var truncated = false;
        foreach (var row in result ?? new List<object?>())
        {
            if (entries.Count >= _config.MaxListItems)
            {
                truncated = true;
                break;
            }

            var fields = AsArray(row);
            if (fields == null || fields.Count < 4)
            {
                continue;
            }

            var commandArgs = AsArray(fields[3]) ?? new List<object?>();
            var command = commandArgs.Count > 0 ? AsString(commandArgs[0]).ToUpperInvariant() : string.Empty;
            var args = string.Join(" ", commandArgs.Skip(1).Select(AsString));
            entries.Add(RedisResponseParser.BuildSlowLogEntry(
                ToLong(fields.Count > 0 ? fields[0] : null),
                ToLong(fields.Count > 1 ? fields[1] : null),
                ToLong(fields.Count > 2 ? fields[2] : null),
                command,
                args,
                fields.Count > 4 ? AsString(fields[4]) : string.Empty,
                fields.Count > 5 ? AsString(fields[5]) : string.Empty));
        }

        return new RedisQueryResponse { ResultType = "slowlog", SlowLog = entries, Truncated = truncated };
    }

    private async Task<RedisQueryResponse> ClientListAsync(RedisRespClient client, CancellationToken cancellationToken)
    {
        var raw = AsString(await client.ExecAsync("CLIENT", new object[] { "LIST" }, cancellationToken).ConfigureAwait(false));
        var clients = RedisResponseParser.ParseClientList(raw);
        var truncated = clients.Count > _config.MaxListItems;
        if (truncated)
        {
            clients = clients.GetRange(0, _config.MaxListItems);
        }

        return new RedisQueryResponse { ResultType = "client_list", Clients = clients, Truncated = truncated };
    }

    private async Task<RedisQueryResponse> ConfigGetAsync(RedisQueryRequest request, RedisRespClient client, CancellationToken cancellationToken)
    {
        var pattern = string.IsNullOrWhiteSpace(request.Pattern) ? "*" : request.Pattern.Trim();
        var pairs = AsArray(await client.ExecAsync("CONFIG", new object[] { "GET", pattern }, cancellationToken).ConfigureAwait(false));
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; pairs != null && i + 1 < pairs.Count; i += 2)
        {
            config[AsString(pairs[i])] = AsString(pairs[i + 1]);
        }

        return new RedisQueryResponse { ResultType = "config_get", Config = config };
    }

    private async Task<RedisQueryResponse> KeyInfoAsync(RedisQueryRequest request, RedisRespClient client, CancellationToken cancellationToken)
    {
        var key = (request.Key ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            throw new BusinessException(400, "key_info 模式需要提供键名 Key");
        }

        var type = AsString(await client.ExecAsync("TYPE", new object[] { key }, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
        if (type == "none")
        {
            return new RedisQueryResponse
            {
                ResultType = "key_info",
                KeyInfo = new RedisKeyInfo { Key = key, Exists = false, Type = type },
            };
        }

        var ttl = ToLong(await client.ExecAsync("TTL", new object[] { key }, cancellationToken).ConfigureAwait(false));
        var lengthCommand = RedisResponseParser.LengthCommandFor(type);
        long? length = lengthCommand == null ? null : ToLong(await client.ExecAsync(lengthCommand, new object[] { key }, cancellationToken).ConfigureAwait(false));
        long? memoryBytes = null;
        try
        {
            var memory = await client.ExecAsync("MEMORY", new object[] { "USAGE", key }, cancellationToken).ConfigureAwait(false);
            memoryBytes = memory == null ? null : ToLong(memory);
        }
        catch (BusinessException)
        {
            // 旧版本 Redis 无 MEMORY 命令：内存占用留空即可.
        }

        return new RedisQueryResponse
        {
            ResultType = "key_info",
            KeyInfo = new RedisKeyInfo
            {
                Key = key,
                Exists = true,
                Type = type,
                TtlSeconds = ttl,
                Length = length,
                MemoryBytes = memoryBytes,
            },
        };
    }
}
