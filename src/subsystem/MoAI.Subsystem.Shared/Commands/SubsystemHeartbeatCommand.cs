using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 子系统心跳（机器面，子系统 token 鉴权）：刷新 Redis 在线在场键，TTL 内未续期视为离线.
/// </summary>
public class SubsystemHeartbeatCommand : IRequest<EmptyCommandResponse>, IModelValidator<SubsystemHeartbeatCommand>
{
    /// <summary>
    /// 子系统 id（由服务端从认证上下文回填，不接受请求体注入）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <summary>
    /// 实例标识（子系统多实例部署时区分实例），可选.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// 子系统上报版本号，可选.
    /// </summary>
    public string? Version { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemHeartbeatCommand> validate)
    {
        validate.RuleFor(x => x.InstanceId).MaximumLength(100).WithMessage("实例标识最长 100 字符.");
        validate.RuleFor(x => x.Version).MaximumLength(50).WithMessage("版本号最长 50 字符.");
    }
}
