using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 删除通知渠道（软删除，历史发送记录保留）.
/// </summary>
public class DeleteNotificationChannelCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteNotificationChannelCommand>
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long Id { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteNotificationChannelCommand> validate)
    {
        validate.RuleFor(x => x.Id).GreaterThan(0).WithMessage("渠道 id 不存在");
    }
}
