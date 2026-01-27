using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询工作流执行详情命令.
/// 用于检索工作流执行的完整历史记录，包括所有节点的执行管道、输入输出和状态.
/// 这是 QueryWorkflowInstanceCommand 的别名，用于更明确地表达查询执行历史的意图.
/// </summary>
public class QueryWorkflowExecutionCommand : IRequest<QueryWorkflowExecutionCommandResponse>
{
    /// <summary>
    /// 工作流执行 ID（历史记录 ID）.
    /// </summary>
    public Guid Id { get; init; }
}
