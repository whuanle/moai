using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 委托 SSO 委托码签发（MoAI 登录用户发起）：校验子系统开放登录接入且用户所在团队已获授权，
/// 返回一次性委托码与注册的回调地址，前端携带 code 回跳子系统.
/// </summary>
public class SubsystemSsoAuthorizeCommand : IUserIdContext, IRequest<SubsystemSsoAuthorizeCommandResponse>, IModelValidator<SubsystemSsoAuthorizeCommand>
{
    /// <summary>
    /// 子系统标识（appid）.
    /// </summary>
    public string SubsystemKey { get; init; } = default!;

    /// <summary>
    /// 子系统自定义透传参数（防 CSRF 等），原样追加到回调地址，可选.
    /// </summary>
    public string? State { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemSsoAuthorizeCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemKey).NotEmpty().WithMessage("子系统标识不能为空.").MaximumLength(40).WithMessage("子系统标识最长 40 字符.");
        validate.RuleFor(x => x.State).MaximumLength(200).WithMessage("透传参数最长 200 字符.")
            .Matches("^[A-Za-z0-9._~:-]*$").WithMessage("透传参数仅允许字母/数字与 _ . ~ : - 字符.");
    }
}
