using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 工作流处理项，表示工作流执行期间流式传输给客户端的节点执行信息.
/// 用于实时监控工作流进度和调试问题.
/// </summary>
public class WorkflowProcessingItem
{
    /// <summary>
    /// 节点类型（Start、End、AiChat、Wiki、Plugin、Condition、ForEach、Fork、JavaScript、DataProcess）.
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点唯一标识符.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点输入数据，键为字段名称，值为字段值.
    /// </summary>
    public Dictionary<string, object> Input { get; set; } = new();

    /// <summary>
    /// 节点输出数据，键为字段名称，值为字段值.
    /// </summary>
    public Dictionary<string, object> Output { get; set; } = new();

    /// <summary>
    /// 节点执行状态（Pending、Running、Completed、Failed）.
    /// </summary>
    public NodeState State { get; set; }

    /// <summary>
    /// 错误消息，当节点执行失败时包含错误详情和堆栈跟踪.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 节点执行时间戳.
    /// </summary>
    public DateTimeOffset ExecutedTime { get; set; }
}
