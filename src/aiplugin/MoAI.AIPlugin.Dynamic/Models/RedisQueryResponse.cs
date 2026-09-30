using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Redis 只读诊断插件响应（按 Mode 只填充对应字段，其余为空）.
/// </summary>
public class RedisQueryResponse
{
    /// <summary>
    /// 结果类型：info / dbsize / slowlog / client_list / config_get / key_info.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// info 模式：按节段归组的运行信息.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? Sections { get; set; }

    /// <summary>
    /// dbsize 模式：当前逻辑库键数量.
    /// </summary>
    public long? DbSize { get; set; }

    /// <summary>
    /// slowlog 模式：慢查询列表.
    /// </summary>
    public IReadOnlyList<RedisSlowLogEntry> SlowLog { get; set; } = new List<RedisSlowLogEntry>();

    /// <summary>
    /// client_list 模式：客户端连接列表（每行一个键值字典）.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Clients { get; set; } = new List<IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// config_get 模式：配置项与值.
    /// </summary>
    public Dictionary<string, string>? Config { get; set; }

    /// <summary>
    /// key_info 模式：单键体检结果.
    /// </summary>
    public RedisKeyInfo? KeyInfo { get; set; }

    /// <summary>
    /// 列表是否因超过 MaxListItems 被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Redis 慢查询条目.
/// </summary>
public class RedisSlowLogEntry
{
    /// <summary>
    /// 条目 ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 命令执行完成时间（ISO 8601 UTC）.
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 执行耗时（微秒）.
    /// </summary>
    public long DurationMicros { get; set; }

    /// <summary>
    /// 命令名（大写）.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 参数（空格拼接，超长截断）.
    /// </summary>
    public string Args { get; set; } = string.Empty;

    /// <summary>
    /// 客户端地址（Redis 4+，可空）.
    /// </summary>
    public string ClientAddress { get; set; } = string.Empty;

    /// <summary>
    /// 客户端名（Redis 4+，可空）.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;
}

/// <summary>
/// Redis 单键体检结果.
/// </summary>
public class RedisKeyInfo
{
    /// <summary>
    /// 键名.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 键是否存在.
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 类型（string/hash/list/set/zset/stream/none 等）.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 剩余过期秒数；-1 表示永不过期；键不存在时为 null.
    /// </summary>
    public long? TtlSeconds { get; set; }

    /// <summary>
    /// 元素个数（按类型取 STRLEN/HLEN/LLEN/SCARD/ZCARD/XLEN；不适用时为 null）.
    /// </summary>
    public long? Length { get; set; }

    /// <summary>
    /// 内存占用字节（MEMORY USAGE；服务端不支持或为空时 null）.
    /// </summary>
    public long? MemoryBytes { get; set; }
}
