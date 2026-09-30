using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Redis 只读诊断插件请求参数.
/// </summary>
public class RedisQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：info=运行信息（默认）/ dbsize=键数量 / slowlog=慢查询 / client_list=客户端连接 / config_get=读配置 / key_info=单键体检；未知值会被拒绝")]
    public string Mode { get; set; } = "info";

    /// <summary>
    /// info 模式：节段（可空=全部）.
    /// </summary>
    [Description("info 模式：节段（可空=全部），如 server/clients/memory/persistence/stats/replication/keyspace")]
    public string? Section { get; set; }

    /// <summary>
    /// slowlog 模式：最多取回条数.
    /// </summary>
    [Description("slowlog 模式：最多取回条数，1-128（默认 25）")]
    public int Count { get; set; } = 25;

    /// <summary>
    /// config_get 模式：配置项通配符.
    /// </summary>
    [Description("config_get 模式：配置项通配符（默认 *），如 maxmemory*")]
    public string Pattern { get; set; } = "*";

    /// <summary>
    /// key_info 模式：目标键名.
    /// </summary>
    [Description("key_info 模式：目标键名")]
    public string Key { get; set; } = string.Empty;
}
