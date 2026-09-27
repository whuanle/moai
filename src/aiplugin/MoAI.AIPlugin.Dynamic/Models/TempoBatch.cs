using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Tempo 链路的批次（资源属性 + spans）.
/// </summary>
public class TempoBatch
{
    /// <summary>
    /// 资源属性（service.name 等进程级标签）.
    /// </summary>
    [Description("资源属性（service.name 等进程级标签）")]
    public IReadOnlyDictionary<string, string> Resource { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 链路 span 列表.
    /// </summary>
    [Description("该批次的 span 列表（含父子关系与属性）")]
    public IReadOnlyList<TempoSpan> Spans { get; set; } = [];
}
