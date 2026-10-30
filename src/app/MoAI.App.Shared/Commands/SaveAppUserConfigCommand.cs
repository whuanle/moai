using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 保存用户级应用配置：用户对某个应用的个性化定制（默认专家/默认技能的勾选），跨会话复用.
/// </summary>
public class SaveAppUserConfigCommand : IRequest<EmptyCommandResponse>, IModelValidator<SaveAppUserConfigCommand>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 用户选择的专家提示词 id，须为本人个人提示词或本团队提示词，0=清除.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 用户勾选启用的技能 id 列表，须为应用默认技能的子集；null=不修改.
    /// </summary>
    public IReadOnlyList<Guid>? Skills { get; init; }

    /// <summary>
    /// 工具审批模式（auto/approval），null=不修改；approval 时重要工具调用前需人工批准.
    /// </summary>
    public string? ToolApprovalMode { get; init; }

    /// <summary>
    /// 长期记忆模式：0=跟随应用默认；1=对我关闭；2=仅本应用内；3=跨应用（需应用允许）；null=不修改.
    /// </summary>
    public int? MemoryMode { get; init; }

    /// <summary>
    /// 全局记忆接入模式：0=默认（应用允许时接入）；1=不接入全局记忆；null=不修改（需应用记忆配置 allowGlobalMemory）.
    /// </summary>
    public int? MemoryGlobalMode { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveAppUserConfigCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.PromptId).GreaterThanOrEqualTo(0).WithMessage("提示词 id 不正确.");
        validate.RuleForEach(x => x.Skills).ChildRules(skills =>
        {
            skills.RuleFor(x => x).NotEmpty().WithMessage("技能 id 不正确.");
        });
        validate.RuleFor(x => x.ToolApprovalMode)
            .Must(x => x == null || MoAI.AI.AppToolApprovalContract.IsValidMode(x))
            .WithMessage("工具审批模式不正确.");
        validate.RuleFor(x => x.MemoryMode)
            .Must(x => x == null || x is >= 0 and <= 3)
            .WithMessage("长期记忆模式不正确.");
        validate.RuleFor(x => x.MemoryGlobalMode)
            .Must(x => x == null || x is >= 0 and <= 1)
            .WithMessage("全局记忆接入模式不正确.");
    }
}
