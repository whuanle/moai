using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// 节点执行结果.
/// </summary>
public class NodeExecutionResult
{
    /// <summary>
    /// 执行状态（Completed 或 Failed）.
    /// </summary>
    public NodeState State { get; set; }

    /// <summary>
    /// 节点输出，键为字段名称（对应节点定义的 Outputs 声明）.
    /// </summary>
    public JsonObject Output { get; set; } = new();

    /// <summary>
    /// 错误信息（Failed 时必填）.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建成功结果.
    /// </summary>
    public static NodeExecutionResult Success(JsonObject? output = null)
    {
        return new NodeExecutionResult
        {
            State = NodeState.Completed,
            Output = output ?? new JsonObject(),
        };
    }

    /// <summary>
    /// 创建失败结果.
    /// </summary>
    public static NodeExecutionResult Failure(string errorMessage)
    {
        return new NodeExecutionResult
        {
            State = NodeState.Failed,
            Output = new JsonObject(),
            ErrorMessage = errorMessage,
        };
    }
}

/// <summary>
/// 节点执行器接口 - 每种节点类型实现一个执行器并向注册表注册.
/// 新增节点类型 = 新增一个实现 + 注册，无需修改调度器（后期按定义扩展各类节点）.
/// </summary>
public interface INodeExecutor
{
    /// <summary>
    /// 此执行器支持的节点类型（对应 NodeTypes 常量或自定义类型）.
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// 执行节点逻辑.
    /// </summary>
    /// <param name="context">执行上下文（输入已由数据传输模块解析）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken);
}
