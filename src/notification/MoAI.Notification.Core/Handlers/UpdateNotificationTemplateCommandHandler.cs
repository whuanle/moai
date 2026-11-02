using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateNotificationTemplateCommand"/>.
/// </summary>
public class UpdateNotificationTemplateCommandHandler : IRequestHandler<UpdateNotificationTemplateCommand, MoAI.Infra.Models.EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotificationTemplateCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateNotificationTemplateCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<MoAI.Infra.Models.EmptyCommandResponse> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        if (NotificationTemplateVariableParser.Parse(request.Variables, out var parseError) == null)
        {
            throw new BusinessException(parseError) { StatusCode = 400 };
        }

        var entity = await _databaseContext.NotificationTemplates
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new BusinessException("通知模板不存在") { StatusCode = 404 };

        var nameExists = await _databaseContext.NotificationTemplates.AsNoTracking()
            .AnyAsync(x => x.Name == request.Name && x.Id != request.Id, cancellationToken);
        if (nameExists)
        {
            throw new BusinessException("通知模板名称已存在") { StatusCode = 400 };
        }

        entity.Name = request.Name;
        entity.Title = request.Title;
        entity.Content = request.Content;
        entity.ContentType = (int)request.ContentType;
        entity.Variables = request.Variables;
        entity.AliyunTemplateCode = request.AliyunTemplateCode;
        entity.TencentTemplateId = request.TencentTemplateId;
        entity.Description = request.Description;

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return MoAI.Infra.Models.EmptyCommandResponse.Default;
    }
}
