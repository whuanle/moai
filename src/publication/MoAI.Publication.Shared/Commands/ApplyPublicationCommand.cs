using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Publication.Models;

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

    /// <summary>
    /// 应用版本号 aaa.bb.cc（如 1.2.0），可空；上架即把最新的团队发布上架，不携带时自动记录应用当前版本号.
    /// 携带时校验格式且不能低于应用当前版本. 存库编码 aaa*10000 + bb*100 + cc（与 MoAI.App.Shared.AppVersionHelper 同规则）.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 可视范围（仅应用上架使用）：all=全员可见，organization=按组织限定；默认全员.
    /// </summary>
    public AppVisibleScope VisibleScope { get; init; } = AppVisibleScope.All;

    /// <summary>
    /// 可视范围组织授权清单（仅 VisibleScope=organization 时使用，同一组织不可重复），审批通过后物化为该应用的组织可见授权.
    /// </summary>
    public IReadOnlyList<VisibleOrgGrantItem> VisibleOrgGrants { get; init; } = [];

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
        validate.RuleFor(x => x.Version).Matches(@"^\d{1,3}\.\d{1,2}\.\d{1,2}$").When(x => !string.IsNullOrWhiteSpace(x.Version))
            .WithMessage("应用版本号格式不正确，应为 aaa.bb.cc（如 1.2.0）.");
        validate.RuleFor(x => x.Version).Empty().When(x => x.ResourceType != PublicationResourceType.App)
            .WithMessage("仅应用申请上架需要版本号.");
        validate.RuleFor(x => x.VisibleScope).IsInEnum().WithMessage("可视范围不正确.");
        validate.RuleFor(x => x.VisibleScope).Equal(AppVisibleScope.All).When(x => x.ResourceType != PublicationResourceType.App)
            .WithMessage("仅应用申请上架支持设置可视范围.");
        validate.RuleFor(x => x.VisibleOrgGrants).Empty().When(x => x.ResourceType != PublicationResourceType.App || x.VisibleScope != AppVisibleScope.Organization)
            .WithMessage("仅按组织限定的可视范围支持组织授权清单.");
        validate.RuleFor(x => x.VisibleOrgGrants).NotEmpty().When(x => x.ResourceType == PublicationResourceType.App && x.VisibleScope == AppVisibleScope.Organization)
            .WithMessage("按组织限定可视范围时组织授权清单不能为空.");
        validate.RuleForEach(x => x.VisibleOrgGrants).ChildRules(item =>
        {
            item.RuleFor(x => x.OrgRoleId).GreaterThan(0).WithMessage("组织授权的组织 id 不正确.");
            item.RuleFor(x => x.Scope).IsInEnum().WithMessage("组织授权方向不正确.");
        });
        validate.RuleFor(x => x.VisibleOrgGrants)
            .Must(grants => grants.Select(x => x.OrgRoleId).Distinct().Count() == grants.Count)
            .When(x => x.ResourceType == PublicationResourceType.App && x.VisibleScope == AppVisibleScope.Organization)
            .WithMessage("同一组织不可重复设置授权.");
    }
}
