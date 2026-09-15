using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Publication.Commands;

/// <summary>
/// 申请上架，将团队资源（应用/提示词）提交到上架审核，由系统管理员审批；
/// 需要资源所属团队的 Admin 及以上角色.
/// </summary>
public class ApplyPublicationCommand : IRequest<SimpleLong>, IUserIdContext, IModelValidator<ApplyPublicationCommand>
{
    /// <summary>
    /// 资源类型.
    /// </summary>
    public PublicationResourceType ResourceType { get; init; }

    /// <summary>
    /// 资源 id 字符串，应用为 app.id（uuid），提示词为 prompt.id（数字）.
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
