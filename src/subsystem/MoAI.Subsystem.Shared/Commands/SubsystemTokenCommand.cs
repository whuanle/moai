using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 系统级接入 token 换取（匿名机器面）：appid + 密钥换短时 JWT，子系统与应用接入通用.
/// </summary>
public class SubsystemTokenCommand : IRequest<SubsystemTokenCommandResponse>, IModelValidator<SubsystemTokenCommand>
{
    /// <summary>
    /// 接入标识（appid）：子系统为创建时自填的标识，应用接入为 moai-sa- 前缀自动生成.
    /// </summary>
    public string AppId { get; init; } = default!;

    /// <summary>
    /// 接入密钥（创建/重置时返回的原文）.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemTokenCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("接入 appid 不能为空.").MaximumLength(255).WithMessage("接入 appid 过长.");
        validate.RuleFor(x => x.Secret).NotEmpty().WithMessage("接入密钥不能为空.").MaximumLength(128).WithMessage("接入密钥最长 128 字符.");
    }
}
