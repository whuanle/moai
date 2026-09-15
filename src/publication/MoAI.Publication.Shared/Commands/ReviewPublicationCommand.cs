using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Publication.Commands;

/// <summary>
/// 审批上架申请（通过/驳回），仅系统管理员可操作；通过后系统将目标资源 is_public 置为 true.
/// </summary>
public class ReviewPublicationCommand : IRequest<EmptyCommandResponse>, IModelValidator<ReviewPublicationCommand>
{
    /// <summary>
    /// 上架审核记录 id.
    /// </summary>
    public long PublicationId { get; init; }

    /// <summary>
    /// 是否通过，true=通过上架，false=驳回.
    /// </summary>
    public bool IsApprove { get; init; }

    /// <summary>
    /// 审批意见，可为空.
    /// </summary>
    public string? ReviewComment { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ReviewPublicationCommand> validate)
    {
        validate.RuleFor(x => x.PublicationId).GreaterThan(0).WithMessage("上架审核记录 id 不正确.");
        validate.RuleFor(x => x.ReviewComment).MaximumLength(255).WithMessage("审批意见最长 255 个字符.");
    }
}
