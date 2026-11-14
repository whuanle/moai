using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="CreateNotificationTemplateCommand"/>.
/// </summary>
public class CreateNotificationTemplateCommandHandler : IRequestHandler<CreateNotificationTemplateCommand, CreateNotificationTemplateCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNotificationTemplateCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateNotificationTemplateCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateNotificationTemplateCommandResponse> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        if (NotificationTemplateVariableParser.Parse(request.Variables, out var parseError) == null)
        {
            throw new BusinessException(parseError) { StatusCode = 400 };
        }

        var nameExists = await _databaseContext.NotificationTemplates.AsNoTracking()
            .AnyAsync(x => x.Name == request.Name, cancellationToken);
        if (nameExists)
        {
            throw new BusinessException("通知模板名称已存在") { StatusCode = 400 };
        }

        var entity = new NotificationTemplateEntity
        {
            Name = request.Name,
            Title = request.Title,
            Content = request.Content,
            ContentType = (int)request.ContentType,
            Variables = request.Variables,
            AliyunTemplateCode = request.AliyunTemplateCode,
            TencentTemplateId = request.TencentTemplateId,
            Description = request.Description
        };

        _databaseContext.NotificationTemplates.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new CreateNotificationTemplateCommandResponse { Id = entity.Id };
    }
}
