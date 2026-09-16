using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Commands;

/// <summary>
/// 保存流程应用编排草稿（流程定义 JSON + 编辑器画布 JSON），需要团队 Admin 及以上角色.
/// 保存后草稿标记为未发布状态，不影响已发布版本.
/// </summary>
public class SaveAppWorkflowDraftCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<SaveAppWorkflowDraftCommand>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 流程定义 JSON（引擎 WorkflowDefinition 契约：nodes + connections + ui），顶层 id 等于应用 id.
    /// </summary>
    public string Definition { get; init; } = default!;

    /// <summary>
    /// 编辑器画布原始 JSON（FlowGram toJSON 产物），用于无损还原画布.
    /// </summary>
    public string EditorData { get; init; } = default!;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveAppWorkflowDraftCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Definition).NotEmpty().WithMessage("流程定义不能为空.");
        validate.RuleFor(x => x.EditorData).NotEmpty().WithMessage("编辑器数据不能为空.");
    }
}
