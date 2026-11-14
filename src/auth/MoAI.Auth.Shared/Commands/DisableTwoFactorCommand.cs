using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 关闭两步验证（2FA）：校验当前动态码后解绑；系统强制开启时拒绝.
/// </summary>
public class DisableTwoFactorCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<DisableTwoFactorCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }


    /// <summary>
    /// 认证器 App 当前动态码（6 位数字）.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DisableTwoFactorCommand> validate)
    {
        // ContextUserId 由 Controller 在动作内回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("动态码为 6 位数字.");
    }
}
