using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 子系统接入 token 换取（匿名机器面）：appid + 密钥换短时 JWT.
/// </summary>
public class SubsystemTokenCommand : IRequest<SubsystemTokenCommandResponse>, IModelValidator<SubsystemTokenCommand>
{
    /// <summary>
    /// 子系统标识（appid）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 接入密钥（创建/重置时返回的原文）.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemTokenCommand> validate)
    {
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("子系统标识不能为空.").MaximumLength(40).WithMessage("子系统标识最长 40 字符.");
        validate.RuleFor(x => x.Secret).NotEmpty().WithMessage("接入密钥不能为空.").MaximumLength(128).WithMessage("接入密钥最长 128 字符.");
    }
}
