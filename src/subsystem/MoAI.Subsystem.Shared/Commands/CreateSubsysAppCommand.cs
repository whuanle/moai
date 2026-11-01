using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 创建系统级接入（仅 root）：appType=subsystem 子系统（appid 自填，可配置委托 SSO 回调与团队授权）/
/// app 应用接入（appid 自动生成 moai-sa- 前缀）。两类均可全量访问 /api/subsys 协作面资源.
/// </summary>
public class CreateSubsysAppCommand : IRequest<CreateSubsysAppCommandResponse>, IModelValidator<CreateSubsysAppCommand>
{
    /// <summary>
    /// 接入类型：subsystem=子系统 app=应用接入.
    /// </summary>
    public AccessAppType AppType { get; init; }

    /// <summary>
    /// 接入标识（appid），仅子系统需要自填且创建后不可修改；应用接入自动生成，忽略该字段.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可选.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 团队授权范围：allTeams=全员可用 specifiedTeams=指定团队（仅子系统，SSO 门禁用）.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 指定团队范围下的授权团队 id 列表，可为空（此时仅组织授权生效）.
    /// </summary>
    public IReadOnlyList<int> TeamIds { get; init; } = [];

    /// <summary>
    /// 委托 SSO 登录回调地址列表（子系统侧完整 URL，最多 10 个，允许测试/生产多入口；仅子系统，空=暂不开放登录接入）.
    /// </summary>
    public IReadOnlyList<string> CallbackUrls { get; init; } = [];

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateSubsysAppCommand> validate)
    {
        validate.RuleFor(x => x.AppType).IsInEnum().WithMessage("接入类型不合法.")
            .Must(x => x == AccessAppType.Subsystem || x == AccessAppType.App).WithMessage("接入类型须为 subsystem 或 app.");
        validate.RuleFor(x => x.Key)
            .NotEmpty().WithMessage("接入标识（appid）不能为空.")
            .Matches("^[a-zA-Z][a-zA-Z0-9_-]{1,39}$").WithMessage("接入标识须以字母开头，仅含字母/数字/下划线/中划线，长度 2~40.")
            .Must(x => !x.StartsWith("moai-sa-") && !x.StartsWith("moai-ac-")).WithMessage("接入标识不能使用 moai- 保留前缀.")
            .When(x => x.AppType == AccessAppType.Subsystem);
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(50).WithMessage("接入名称最长 50 字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 字符.");
        validate.RuleFor(x => x.TeamScope).IsInEnum().WithMessage("团队授权范围不合法.");
        validate.RuleForEach(x => x.TeamIds).GreaterThan(0).WithMessage("团队 id 不合法.");
        validate.RuleForEach(x => x.CallbackUrls).Must(x => x.Length <= 255).WithMessage("回调地址最长 255 字符.")
            .Matches("^https?://.+").WithMessage("回调地址须为 http(s) 完整地址.")
            .When(x => x.CallbackUrls.Count > 0);
        validate.RuleFor(x => x.CallbackUrls).Must(x => x.Count <= 10).WithMessage("回调地址最多 10 个.");
    }
}
