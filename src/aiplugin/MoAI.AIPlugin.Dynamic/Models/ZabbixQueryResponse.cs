using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Zabbix 查询插件响应（按 Mode 只填充对应集合，其余为空）.
/// </summary>
public class ZabbixQueryResponse
{
    /// <summary>
    /// 结果类型：version / problems / hosts / triggers.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// version 模式：Zabbix 服务端版本号.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// problems 模式：当前问题列表.
    /// </summary>
    public IReadOnlyList<ZabbixProblem> Problems { get; set; } = new List<ZabbixProblem>();

    /// <summary>
    /// hosts 模式：主机列表.
    /// </summary>
    public IReadOnlyList<ZabbixHost> Hosts { get; set; } = new List<ZabbixHost>();

    /// <summary>
    /// triggers 模式：问题态触发器列表.
    /// </summary>
    public IReadOnlyList<ZabbixTrigger> Triggers { get; set; } = new List<ZabbixTrigger>();

    /// <summary>
    /// 列表是否因超过 MaxListItems 被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Zabbix 当前问题.
/// </summary>
public class ZabbixProblem
{
    /// <summary>
    /// 事件 ID.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// 严重级数值（0-5）.
    /// </summary>
    public int Severity { get; set; }

    /// <summary>
    /// 严重级名称（未分类/信息/警告/一般/严重/灾难）.
    /// </summary>
    public string SeverityName { get; set; } = string.Empty;

    /// <summary>
    /// 问题名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 发生时间（ISO 8601 UTC）.
    /// </summary>
    public string Clock { get; set; } = string.Empty;

    /// <summary>
    /// 已持续时长（人读格式，如 3d 4h 12m）.
    /// </summary>
    public string Age { get; set; } = string.Empty;

    /// <summary>
    /// 是否已确认.
    /// </summary>
    public bool Acknowledged { get; set; }

    /// <summary>
    /// 关联主机 ID.
    /// </summary>
    public string HostId { get; set; } = string.Empty;

    /// <summary>
    /// 关联主机可见名（经 trigger.get 富化）.
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// 事件标签.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Zabbix 主机.
/// </summary>
public class ZabbixHost
{
    /// <summary>
    /// 主机 ID.
    /// </summary>
    public string HostId { get; set; } = string.Empty;

    /// <summary>
    /// 技术主机名.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 可见名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 监控状态：0=已监控 1=停用.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 接口列表（ip/dns/port/type/available）.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Interfaces { get; set; } = new List<IReadOnlyDictionary<string, string>>();
}

/// <summary>
/// Zabbix 问题态触发器.
/// </summary>
public class ZabbixTrigger
{
    /// <summary>
    /// 触发器 ID.
    /// </summary>
    public string TriggerId { get; set; } = string.Empty;

    /// <summary>
    /// 触发器描述（问题名）.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 严重级数值（0-5）.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 严重级名称.
    /// </summary>
    public string PriorityName { get; set; } = string.Empty;

    /// <summary>
    /// 状态值：1=PROBLEM.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// 最后一次状态变化时间（ISO 8601 UTC）.
    /// </summary>
    public string LastChange { get; set; } = string.Empty;

    /// <summary>
    /// 已持续时长（人读格式）.
    /// </summary>
    public string Age { get; set; } = string.Empty;

    /// <summary>
    /// 关联主机（host/host/name）.
    /// </summary>
    public IReadOnlyList<ZabbixHostRef> Hosts { get; set; } = new List<ZabbixHostRef>();
}

/// <summary>
/// Zabbix 触发器关联主机引用.
/// </summary>
public class ZabbixHostRef
{
    /// <summary>
    /// 主机 ID.
    /// </summary>
    public string HostId { get; set; } = string.Empty;

    /// <summary>
    /// 技术主机名.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 可见名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
