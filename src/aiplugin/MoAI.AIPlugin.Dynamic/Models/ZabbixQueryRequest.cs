using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Zabbix 查询插件请求参数.
/// </summary>
public class ZabbixQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：version=服务端版本（免鉴权）/ problems=当前问题（默认）/ hosts=主机列表 / triggers=问题态触发器；未知值会被拒绝")]
    public string Mode { get; set; } = "problems";

    /// <summary>
    /// 最低严重级（0-5）.
    /// </summary>
    [Description("最低严重级 0=未分类 1=信息 2=警告 3=一般 4=严重 5=灾难；problems/triggers 仅返回 ≥ 该级别的条目（默认 0）")]
    public int SeverityMin { get; set; }

    /// <summary>
    /// 主机名模糊过滤（可空）.
    /// </summary>
    [Description("主机名模糊过滤（可空）：hosts/triggers 模式按主机名搜索；problems 模式忽略")]
    public string? SearchHost { get; set; }

    /// <summary>
    /// 主机群组 ID 过滤（可空）.
    /// </summary>
    [Description("主机群组 ID 过滤（可空）：逗号分隔，如 2,5；hosts/triggers 模式生效")]
    public string? HostGroupIds { get; set; }
}
