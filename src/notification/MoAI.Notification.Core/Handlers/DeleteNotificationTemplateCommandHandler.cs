using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteNotificationTemplateCommand"/>.
/// </summary>
public class DeleteNotificationTemplateCommandHandler : IRequestHandler<DeleteNotificationTemplateCommand, MoAI.Infra.Models.EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteNotificationTemplateCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteNotificationTemplateCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<MoAI.Infra.Models.EmptyCommandResponse> Handle(DeleteNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.NotificationTemplates
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("通知模板不存在") { StatusCode = 404 };
        }

        // 内置模板（验证码邮件等）随种子提供，业务按 code 回退解析，删除会破坏未绑定时的自动回退
        if (!string.IsNullOrEmpty(entity.Code))
        {
            throw new BusinessException("内置通知模板不可删除，可编辑内容适配需求") { StatusCode = 400 };
        }

        // 软删除由框架审计钩子完成；历史发送记录与已入队消息（渲染后文本）不受影响
        _databaseContext.NotificationTemplates.Remove(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return MoAI.Infra.Models.EmptyCommandResponse.Default;
    }
}
