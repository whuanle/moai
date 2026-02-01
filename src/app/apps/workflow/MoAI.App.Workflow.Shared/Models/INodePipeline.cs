using System.Text.Json;
using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 节点管道接口，表示节点的执行上下文和记录.
/// 包含执行状态、输入输出数据、错误信息以及参数解析所需的变量映射.
/// 节点执行时应通过此接口获取参数，而不是直接访问 IWorkflowContext.
/// </summary>
public interface INodePipeline
{
    /// <summary>
    /// 节点执行状态（Pending、Running、Completed、Failed）.
    /// </summary>
    NodeState State { get; }

    /// <summary>
    /// 输入数据的 JSON 元素表示.
    /// 保留原始 JSON 结构用于序列化和存储.
    /// </summary>
    JsonElement InputJsonElement { get; }

    /// <summary>
    /// 输入数据的映射表示，键为字段名称，值为字段值.
    /// 用于程序内部访问和处理.
    /// </summary>
    Dictionary<string, object> InputJsonMap { get; }

    /// <summary>
    /// 输出数据的 JSON 元素表示.
    /// 保留原始 JSON 结构用于序列化和存储.
    /// </summary>
    JsonElement OutputJsonElement { get; }

    /// <summary>
    /// 输出数据的映射表示，键为字段名称，值为字段值.
    /// 用于程序内部访问和处理.
    /// </summary>
    Dictionary<string, object> OutputJsonMap { get; }

    /// <summary>
    /// 错误消息，当节点执行失败时包含错误详情和堆栈跟踪.
    /// </summary>
    string ErrorMessage { get; }

    /// <summary>
    /// 扁平化的变量映射，用于表达式解析.
    /// 包括系统变量（sys.*）、启动参数（input.*）和已执行节点的输出（nodeKey.*）.
    /// 此映射在节点执行前从 WorkflowContext 复制而来，确保每个节点有独立的参数上下文.
    /// </summary>
    Dictionary<string, object> FlattenedVariables { get; }

    /// <summary>
    /// 系统变量，从 WorkflowContext 复制而来.
    /// </summary>
    Dictionary<string, object> SystemVariables { get; }

    /// <summary>
    /// 运行时参数（启动参数），从 WorkflowContext 复制而来.
    /// </summary>
    Dictionary<string, object> RuntimeParameters { get; }

    /// <summary>
    /// 已执行节点的输出映射，键为节点键，值为节点输出.
    /// </summary>
    Dictionary<string, Dictionary<string, object>> NodeOutputs { get; }
}
