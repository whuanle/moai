using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 更新子系统（仅 root）：名称/描述/状态/团队授权范围，授权团队列表整体覆盖.
/// </summary>
public class UpdateSubsystemCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateSubsystemCommand>
{
    /// <summary>
    /// 子系统 id（由路由回填）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <summary>
    /// 子系统名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可选.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 状态：enabled=启用 disabled=停用.
    /// </summary>
    public SubsystemStatus Status { get; init; }

    /// <summary>
    /// 团队授权范围：allTeams=全员可用 specifiedTeams=指定团队.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 指定团队范围下的授权团队 id 列表，specifiedTeams 时必填，整体覆盖.
    /// </summary>
    public IReadOnlyList<int> TeamIds { get; init; } = [];

    /// <summary>
    /// 委托 SSO 登录回调地址（子系统侧完整 URL），空=暂不开放登录接入.
    /// </summary>
    public string CallbackUrl { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateSubsystemCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("子系统名称不能为空.").MaximumLength(50).WithMessage("子系统名称最长 50 字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 字符.");
        validate.RuleFor(x => x.Status).IsInEnum().WithMessage("状态不合法.");
        validate.RuleFor(x => x.TeamScope).IsInEnum().WithMessage("团队授权范围不合法.");
        validate.RuleFor(x => x.TeamIds)
            .NotEmpty().WithMessage("指定团队范围必须至少授权一个团队.").When(x => x.TeamScope == SubsystemTeamScope.SpecifiedTeams);
        validate.RuleForEach(x => x.TeamIds).GreaterThan(0).WithMessage("团队 id 不合法.");
        validate.RuleFor(x => x.CallbackUrl)
            .MaximumLength(255).WithMessage("回调地址最长 255 字符.")
            .Matches("^$|^https?://.+").WithMessage("回调地址须为 http(s) 完整地址.");
    }
}
