using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// 节点执行结果模型，包含节点执行后的状态、输出和错误信息.
/// </summary>
public class NodeExecutionResult
{
    /// <summary>
    /// 节点执行状态（Pending、Running、Completed、Failed）.
    /// </summary>
    public NodeState State { get; set; }

    /// <summary>
    /// 节点输出数据，键为字段名称，值为字段值.
    /// 当节点成功执行时，包含节点产生的输出数据.
    /// </summary>
    public Dictionary<string, object> Output { get; set; } = new();

    /// <summary>
    /// 错误消息，当节点执行失败时包含错误详情和堆栈跟踪.
    /// 当 State 为 Failed 时，此字段应包含有意义的错误信息.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建一个成功的执行结果.
    /// </summary>
    /// <param name="output">节点输出数据.</param>
    /// <returns>状态为 Completed 的执行结果.</returns>
    public static NodeExecutionResult Success(Dictionary<string, object> output)
    {
        return new NodeExecutionResult
        {
            State = NodeState.Completed,
            Output = output
        };
    }

    /// <summary>
    /// 创建一个失败的执行结果.
    /// </summary>
    /// <param name="errorMessage">错误消息.</param>
    /// <returns>状态为 Failed 的执行结果.</returns>
    public static NodeExecutionResult Failure(string errorMessage)
    {
        return new NodeExecutionResult
        {
            State = NodeState.Failed,
            Output = new Dictionary<string, object>(),
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// 创建一个失败的执行结果，包含异常信息.
    /// </summary>
    /// <param name="exception">异常对象.</param>
    /// <returns>状态为 Failed 的执行结果.</returns>
    public static NodeExecutionResult Failure(Exception exception)
    {
        return new NodeExecutionResult
        {
            State = NodeState.Failed,
            Output = new Dictionary<string, object>(),
            ErrorMessage = $"{exception.Message}\n{exception.StackTrace}"
        };
    }
}
