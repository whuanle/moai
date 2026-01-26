using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 节点定义接口，定义节点的基本结构和输入输出字段.
/// </summary>
public interface INodeDefine
{
    /// <summary>
    /// 节点唯一标识符.
    /// </summary>
    string NodeKey { get; }

    /// <summary>
    /// 节点类型.
    /// </summary>
    NodeType NodeType { get; }

    /// <summary>
    /// 输入字段定义列表.
    /// </summary>
    IReadOnlyList<FieldDefine> InputFields { get; }

    /// <summary>
    /// 输出字段定义列表.
    /// </summary>
    IReadOnlyList<FieldDefine> OutputFields { get; }
}
