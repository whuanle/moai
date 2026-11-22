using FluentValidation;
using Mediator;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 委托 SSO 进入前预检（匿名可调用）：校验子系统存在且启用、已开放登录回调，
/// 以及当前访问者是否获授权；用于在跳转登录页/回跳子系统之前拦截不可用场景。
/// 登录用户由 <see cref="IUserIdContext.ContextUserId"/> 判定（0=匿名）。
/// </summary>
public class QuerySubsystemSsoAccessCommand : IUserIdContext, IRequest<QuerySubsystemSsoAccessCommandResponse>, IModelValidator<QuerySubsystemSsoAccessCommand>
{
    /// <summary>
    /// 子系统标识（appid）.
    /// </summary>
    public string SubsystemKey { get; init; } = default!;

    /// <summary>
    /// 本次计划回跳的回调地址（子系统多入口），可选；传入时须在注册列表内.
    /// </summary>
    public string? RedirectUri { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsystemSsoAccessCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemKey).NotEmpty().WithMessage("子系统标识不能为空.").MaximumLength(40).WithMessage("子系统标识最长 40 字符.");
        validate.RuleFor(x => x.RedirectUri).MaximumLength(255).WithMessage("回调地址最长 255 字符.");
    }
}
