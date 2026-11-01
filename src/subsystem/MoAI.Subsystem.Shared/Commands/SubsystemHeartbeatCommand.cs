using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 系统级接入心跳（机器面，接入 token 鉴权）：按实例记录 Redis 在场（ZSET），判定窗口内未续期视为离线.
/// </summary>
public class SubsystemHeartbeatCommand : IRequest<EmptyCommandResponse>, IModelValidator<SubsystemHeartbeatCommand>
{
    /// <summary>
    /// 接入 id（由服务端从认证上下文回填，不接受请求体注入）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <summary>
    /// 实例标识（多实例部署时每实例启动生成并保持稳定，服务端按实例记录在场）.
    /// </summary>
    public string InstanceId { get; init; } = default!;

    /// <summary>
    /// 上报版本号，可选.
    /// </summary>
    public string? Version { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubsystemHeartbeatCommand> validate)
    {
        validate.RuleFor(x => x.InstanceId).NotEmpty().WithMessage("实例标识不能为空.").MaximumLength(64).WithMessage("实例标识最长 64 字符.");
        validate.RuleFor(x => x.Version).MaximumLength(64).WithMessage("版本号最长 64 字符.");
    }
}
