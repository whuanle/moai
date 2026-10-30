using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 用户委托 token 刷新（机器面，子系统 token 鉴权）：refresh 换新 access/refresh.
/// </summary>
public class SubsystemSsoRefreshCommand : IRequest<SubsystemSsoExchangeCommandResponse>, IModelValidator<SubsystemSsoRefreshCommand>
{
    /// <summary>
    /// 子系统 id（由服务端从认证上下文回填，不接受请求体注入）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <summary>
    /// 用户委托 refresh token.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemSsoRefreshCommand> validate)
    {
        validate.RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refresh token 不能为空.");
    }
}
