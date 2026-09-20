using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 对会话中挂起等待人工审批的工具调用做出决策（批准/拒绝），
/// 由对话页审批卡触发；仅会话归属用户可操作.
/// </summary>
public class DecideAppSessionToolApprovalCommand : IRequest<DecideAppSessionToolApprovalResponse>, IUserIdContext, IModelValidator<DecideAppSessionToolApprovalCommand>
{
    /// <summary>
    /// 会话 id（由路由回填）.
    /// </summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>
    /// 真实工具名（call_tool 元工具的内层 toolName）.
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// true=批准执行；false=拒绝.
    /// </summary>
    public bool Approved { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DecideAppSessionToolApprovalCommand> validate)
    {
        validate.RuleFor(x => x.ToolName).NotEmpty().MaximumLength(100).WithMessage("工具名不正确.");
    }
}

/// <summary>
/// 审批决策结果：approved/rejected 表示已生效；missing 表示无匹配的待审批记录.
/// </summary>
public class DecideAppSessionToolApprovalResponse
{
    /// <summary>
    /// 决策后的状态：approved/rejected/missing.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}
