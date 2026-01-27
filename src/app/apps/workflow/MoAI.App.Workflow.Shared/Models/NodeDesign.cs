using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 节点设计模型，表示用户对工作流中节点的配置.
/// </summary>
public class NodeDesign
{
    /// <summary>
    /// 节点唯一标识符.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点描述信息.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public NodeType NodeType { get; set; }

    /// <summary>
    /// 下一个节点的标识符（用于定义节点连接）.
    /// </summary>
    public string NextNodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 字段设计映射，键为字段名称，值为字段设计配置.
    /// </summary>
    public Dictionary<string, FieldDesign> FieldDesigns { get; set; } = new();
}
