using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 更新系统级接入（仅 root）：名称/描述/状态；子系统行另支持团队授权范围与回调地址（团队清单整体覆盖），
/// 应用接入行忽略子系统专属字段；appid 不可变更（删除后重建）.
/// </summary>
public class UpdateSubsysAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateSubsysAppCommand>
{
    /// <summary>
    /// 接入 id（由路由回填）.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可选.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 状态：enabled=启用 disabled=停用.
    /// </summary>
    public AccessAppStatus Status { get; init; }

    /// <summary>
    /// 团队授权范围：allTeams=全员可用 specifiedTeams=指定团队（仅子系统行生效）.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 指定团队范围下的授权团队 id 列表，可为空（此时仅组织授权生效），整体覆盖（仅子系统行生效）.
    /// </summary>
    public IReadOnlyList<int> TeamIds { get; init; } = [];

    /// <summary>
    /// 委托 SSO 登录回调地址列表（子系统侧完整 URL，最多 10 个），整体覆盖（仅子系统行生效）.
    /// </summary>
    public IReadOnlyList<string> CallbackUrls { get; init; } = [];

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateSubsysAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(50).WithMessage("接入名称最长 50 字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 字符.");
        validate.RuleFor(x => x.Status).IsInEnum().WithMessage("状态不合法.");
        validate.RuleFor(x => x.TeamScope).IsInEnum().WithMessage("团队授权范围不合法.");
        validate.RuleForEach(x => x.TeamIds).GreaterThan(0).WithMessage("团队 id 不合法.");
        validate.RuleForEach(x => x.CallbackUrls).Must(x => x.Length <= 255).WithMessage("回调地址最长 255 字符.")
            .Matches("^https?://.+").WithMessage("回调地址须为 http(s) 完整地址.")
            .When(x => x.CallbackUrls.Count > 0);
        validate.RuleFor(x => x.CallbackUrls).Must(x => x.Count <= 10).WithMessage("回调地址最多 10 个.");
    }
}
