using System.Text.Json;

namespace MoAI.Workflow.Models;

/// <summary>
/// 工作流上下文接口，提供只读的运行时上下文信息.
/// 包含执行状态、变量和节点输出等信息.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// 工作流实例唯一标识符.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// 工作流定义唯一标识符.
    /// </summary>
    string DefinitionId { get; }

    /// <summary>
    /// 运行时参数，包含工作流启动时传入的参数.
    /// </summary>
    Dictionary<string, object> RuntimeParameters { get; }

    /// <summary>
    /// 已执行的节点键集合，用于跟踪执行历史.
    /// </summary>
    HashSet<string> ExecutedNodeKeys { get; }

    /// <summary>
    /// 节点管道映射，键为节点键，值为节点执行记录.
    /// 包含每个已执行节点的输入、输出和状态信息.
    /// </summary>
    Dictionary<string, INodePipeline> NodePipelines { get; }

    /// <summary>
    /// 扁平化的变量映射，用于快速访问上下文中的所有变量.
    /// 包括系统变量（sys.*）、启动参数（input.*）和节点输出（nodeKey.*）.
    /// 嵌套 JSON 对象被扁平化为点符号格式（如 nodeA.result.name）.
    /// </summary>
    Dictionary<string, object> FlattenedVariables { get; }
}
