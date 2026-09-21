using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 知识图谱只读查询插件配置（每个实例独立保存）.
/// </summary>
public class KgCypherQueryConfig
{
    /// <summary>
    /// 绑定的知识图谱 id。命名口径：读侧/KgCypher 族统一用 KgId，写侧 Command 用 KnowledgeGraphId.
    /// </summary>
    [Description("要绑定的知识图谱 id（本团队的托管图或接入图）")]
    public long KgId { get; set; }

    /// <summary>
    /// 单次最多返回行数.
    /// </summary>
    [Description("单次最多返回行数，取值 1-1000（默认 200）；超出部分被丢弃，并把响应中的 Truncated 置为 true")]
    public int MaxRows { get; set; } = 200;

    /// <summary>
    /// 查询超时秒数.
    /// </summary>
    [Description("查询超时秒数，取值 1-300（默认 30）；超时后查询被取消并返回可读失败")]
    public int TimeoutSeconds { get; set; } = 30;
}
