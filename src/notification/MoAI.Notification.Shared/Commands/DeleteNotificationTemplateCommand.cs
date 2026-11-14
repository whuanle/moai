using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 删除通知模板（软删除，历史发送记录保留）.
/// </summary>
public class DeleteNotificationTemplateCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteNotificationTemplateCommand>
{
    /// <summary>
    /// 模板 id.
    /// </summary>
    public long Id { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteNotificationTemplateCommand> validate)
    {
        validate.RuleFor(x => x.Id).GreaterThan(0).WithMessage("模板 id 不存在");
    }
}
