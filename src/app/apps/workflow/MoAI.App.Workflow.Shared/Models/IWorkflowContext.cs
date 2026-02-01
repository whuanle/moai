using System.Text.Json;

namespace MoAI.Workflow.Models;

/// <summary>
/// 工作流上下文接口，提供只读的运行时上下文信息.
/// 包含执行状态、系统变量和启动参数.
/// 注意：不包含节点输出的扁平化变量，节点参数解析应通过 INodePipeline 进行.
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
    /// 系统变量，包含工作流运行时的系统级信息.
    /// 如 sys.userId、sys.timestamp 等.
    /// </summary>
    Dictionary<string, object> SystemVariables { get; }

    /// <summary>
    /// 运行时参数，包含工作流启动时传入的参数.
    /// 可通过 input.* 格式访问.
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
}
