using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 子系统机器 token 刷新（匿名机器面）：refresh token 轮换新 access/refresh 对，免重发 appid+密钥.
/// </summary>
public class SubsystemTokenRefreshCommand : IRequest<SubsystemTokenCommandResponse>, IModelValidator<SubsystemTokenRefreshCommand>
{
    /// <summary>
    /// 换取 token 时返回的刷新 token.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemTokenRefreshCommand> validate)
    {
        validate.RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("刷新 token 不能为空.").MaximumLength(4096).WithMessage("刷新 token 过长.");
    }
}
