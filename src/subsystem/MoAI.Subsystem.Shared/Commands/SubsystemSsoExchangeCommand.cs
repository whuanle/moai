using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 委托码兑换用户委托 token（机器面，子系统 token 鉴权）：一次性消费，返回 access/refresh 与用户基本资料.
/// </summary>
public class SubsystemSsoExchangeCommand : IRequest<SubsystemSsoExchangeCommandResponse>, IModelValidator<SubsystemSsoExchangeCommand>
{
    /// <summary>
    /// 子系统 id（由服务端从认证上下文回填，不接受请求体注入）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <summary>
    /// 一次性委托码.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemSsoExchangeCommand> validate)
    {
        validate.RuleFor(x => x.Code).NotEmpty().WithMessage("委托码不能为空.").MaximumLength(128).WithMessage("委托码最长 128 字符.");
    }
}
