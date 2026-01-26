using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 节点定义实现类，提供节点的完整定义信息.
/// </summary>
public class NodeDefine : INodeDefine
{
    /// <summary>
    /// 节点唯一标识符.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public NodeType NodeType { get; set; }

    /// <summary>
    /// 输入字段定义列表.
    /// </summary>
    public IReadOnlyList<FieldDefine> InputFields { get; set; } = Array.Empty<FieldDefine>();

    /// <summary>
    /// 输出字段定义列表.
    /// </summary>
    public IReadOnlyList<FieldDefine> OutputFields { get; set; } = Array.Empty<FieldDefine>();
}
