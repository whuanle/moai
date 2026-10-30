using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 创建子系统（仅 root），成功后返回接入密钥原文（仅此一次）.
/// </summary>
public class CreateSubsystemCommand : IRequest<CreateSubsystemCommandResponse>, IModelValidator<CreateSubsystemCommand>
{
    /// <summary>
    /// 子系统标识（appid），创建后不可修改.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 子系统名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可选.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 团队授权范围：allTeams=全员可用 specifiedTeams=指定团队.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 指定团队范围下的授权团队 id 列表，specifiedTeams 时必填.
    /// </summary>
    public IReadOnlyList<int> TeamIds { get; init; } = [];

    /// <summary>
    /// 委托 SSO 登录回调地址（子系统侧完整 URL），空=暂不开放登录接入.
    /// </summary>
    public string CallbackUrl { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateSubsystemCommand> validate)
    {
        validate.RuleFor(x => x.Key)
            .NotEmpty().WithMessage("子系统标识不能为空.")
            .Matches("^[a-zA-Z][a-zA-Z0-9_-]{1,39}$").WithMessage("子系统标识须以字母开头，仅含字母/数字/下划线/中划线，长度 2~40.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("子系统名称不能为空.").MaximumLength(50).WithMessage("子系统名称最长 50 字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 字符.");
        validate.RuleFor(x => x.TeamScope).IsInEnum().WithMessage("团队授权范围不合法.");
        validate.RuleFor(x => x.TeamIds)
            .NotEmpty().WithMessage("指定团队范围必须至少授权一个团队.").When(x => x.TeamScope == SubsystemTeamScope.SpecifiedTeams);
        validate.RuleForEach(x => x.TeamIds).GreaterThan(0).WithMessage("团队 id 不合法.");
        validate.RuleFor(x => x.CallbackUrl)
            .MaximumLength(255).WithMessage("回调地址最长 255 字符.")
            .Matches("^$|^https?://.+").WithMessage("回调地址须为 http(s) 完整地址.");
    }
}
