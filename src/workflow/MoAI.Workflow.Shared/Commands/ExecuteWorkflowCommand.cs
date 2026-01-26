using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 执行工作流命令.
/// 用于执行指定的工作流定义，通过流式传输实时返回节点执行结果.
/// 返回 IAsyncEnumerable 以支持服务器发送事件（SSE）流式响应.
/// </summary>
public class ExecuteWorkflowCommand : IStreamRequest<WorkflowProcessingItem>, IUserIdContext, IModelValidator<ExecuteWorkflowCommand>
{
    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 启动参数，传递给工作流的初始输入数据.
    /// 可通过 input.* 变量在节点中引用这些参数.
    /// </summary>
    public Dictionary<string, object> StartupParameters { get; set; } = new();

    /// <summary>
    /// 系统变量（可选），用于传递系统级别的上下文信息.
    /// 可通过 sys.* 变量在节点中引用这些参数.
    /// </summary>
    public Dictionary<string, object>? SystemVariables { get; set; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExecuteWorkflowCommand> validate)
    {
        validate.RuleFor(x => x.WorkflowDefinitionId)
            .NotEmpty().WithMessage("工作流定义 ID 不能为空");

        validate.RuleFor(x => x.StartupParameters)
            .NotNull().WithMessage("启动参数不能为空");
    }
}
