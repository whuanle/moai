using System;
using System.Net;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 手动封禁 IP（管理员），仅管理员.
/// </summary>
public class AddIpBanCommand : IRequest<EmptyCommandResponse>, IModelValidator<AddIpBanCommand>
{
    /// <summary>
    /// 要封禁的 IP.
    /// </summary>
    public string Ip { get; init; } = string.Empty;

    /// <summary>
    /// 封禁时长（分钟），1~525600.
    /// </summary>
    public int Minutes { get; init; }

    /// <summary>
    /// 封禁原因.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddIpBanCommand> validate)
    {
        validate.RuleFor(x => x.Ip)
            .NotEmpty().WithMessage("IP 不能为空")
            .Must(ip => IPAddress.TryParse(ip, out _)).WithMessage("IP 格式无效");
        validate.RuleFor(x => x.Minutes)
            .InclusiveBetween(1, 525600).WithMessage("封禁时长必须是 1~525600 之间的整数（分钟）");
        validate.RuleFor(x => x.Reason)
            .MaximumLength(200).WithMessage("封禁原因长度不能超过 200 个字符");
    }
}
