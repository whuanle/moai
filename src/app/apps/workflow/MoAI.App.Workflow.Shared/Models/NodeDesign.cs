using MoAI.Infra.Models;
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
    /// 下游节点的标识符列表，用于流程流转.
    /// 一般节点只有一个下游节点，但是条件判断节点等特殊节点可以配置多个下游节点.
    /// </summary>
    public IReadOnlyCollection<string> NextNodeKeys { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 输入参数，字段设计映射，键为字段名称，值为字段设计配置.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, FieldDesign>> InputFieldDesigns { get; set; } = Array.Empty<KeyValue<string, FieldDesign>>();

    /// <summary>
    /// 输出参数，字段设计映射，键为字段名称，值为字段设计配置.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, FieldDesign>> OutputFieldDesigns { get; set; } = Array.Empty<KeyValue<string, FieldDesign>>();
}
