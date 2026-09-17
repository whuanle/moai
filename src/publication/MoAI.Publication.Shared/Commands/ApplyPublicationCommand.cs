using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Publication.Commands;

/// <summary>
/// 申请上架，将资源（应用/提示词/技能）提交到上架审核，由系统管理员审批；
/// 团队资源需要所属团队 Admin 及以上角色，个人资源仅创建人.
/// </summary>
public class ApplyPublicationCommand : IRequest<SimpleLong>, IUserIdContext, IModelValidator<ApplyPublicationCommand>
{
    /// <summary>
    /// 资源类型.
    /// </summary>
    public PublicationResourceType ResourceType { get; init; }

    /// <summary>
    /// 资源 id 字符串，应用/技能为资源 id（uuid），提示词为 prompt.id（数字）.
    /// </summary>
    public string ResourceId { get; init; } = default!;

    /// <summary>
    /// 申请说明，可为空.
    /// </summary>
    public string? ApplyReason { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ApplyPublicationCommand> validate)
    {
        validate.RuleFor(x => x.ResourceType).IsInEnum().WithMessage("资源类型不正确.");
        validate.RuleFor(x => x.ResourceId).NotEmpty().WithMessage("资源 id 不能为空.").MaximumLength(64).WithMessage("资源 id 最长 64 个字符.");
        validate.RuleFor(x => x.ApplyReason).MaximumLength(255).WithMessage("申请说明最长 255 个字符.");
    }
}
