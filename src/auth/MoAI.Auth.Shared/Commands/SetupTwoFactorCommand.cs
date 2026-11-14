using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Auth.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 生成两步验证（2FA）密钥：密钥加密落库但未启用，返回密钥与 otpauth URI 供认证器 App 绑定，需随后调 enable 验证动态码激活.
/// </summary>
public class SetupTwoFactorCommand : IRequest<SetupTwoFactorCommandResponse>, IUserIdContext, IModelValidator<SetupTwoFactorCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }


    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetupTwoFactorCommand> validate)
    {
    }
}
