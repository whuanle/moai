using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Notification.Models;
using MoAI.Notification.Services;

namespace MoAI.Notification.Services;

/// <summary>
/// <inheritdoc cref="INotificationTemplateRenderService"/>
/// </summary>
[InjectOnScoped]
public class NotificationTemplateRenderService : INotificationTemplateRenderService
{
    private readonly DatabaseContext _databaseContext;
    private readonly NotificationTemplateApplier _templateApplier;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationTemplateRenderService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="templateApplier">通知模板应用器.</param>
    public NotificationTemplateRenderService(DatabaseContext databaseContext, NotificationTemplateApplier templateApplier)
    {
        _databaseContext = databaseContext;
        _templateApplier = templateApplier;
    }

    /// <inheritdoc/>
    public async Task<RenderedNotificationTemplate> RenderByIdAsync(long templateId, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default)
    {
        var message = await _templateApplier.ApplyAsync(
            new NotificationMessage
            {
                TemplateId = templateId,
                TemplateVariables = values
            },
            cancellationToken);
        return ToRendered(message);
    }

    /// <inheritdoc/>
    public async Task<RenderedNotificationTemplate?> RenderByBuiltinCodeAsync(string builtinCode, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default)
    {
        var templateId = await _databaseContext.NotificationTemplates.AsNoTracking()
            .Where(x => x.Code == builtinCode)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (templateId == null)
        {
            return null;
        }

        return await RenderByIdAsync(templateId.Value, values, cancellationToken);
    }

    private static RenderedNotificationTemplate ToRendered(NotificationMessage message)
    {
        return new RenderedNotificationTemplate
        {
            Title = message.Title,
            Content = message.Content,
            ContentType = message.ContentType
        };
    }
}
