using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Message.Models;
using MoAI.Message.Services;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// 站内推送渠道发送器：复用站内信，按渠道配置的用户集合/管理员投递.
/// </summary>
public class InSiteChannelSender : INotificationChannelSender
{
    private const string RootSettingKey = "root";

    private readonly DatabaseContext _databaseContext;
    private readonly IMessageService _messageService;
    private readonly ILogger<InSiteChannelSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InSiteChannelSender"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="messageService">站内信领域服务.</param>
    /// <param name="logger">日志.</param>
    public InSiteChannelSender(DatabaseContext databaseContext, IMessageService messageService, ILogger<InSiteChannelSender> logger)
    {
        _databaseContext = databaseContext;
        _messageService = messageService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.InSite;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<InSiteChannelConfig>(channel.Config);

        var userIds = new List<long>(config.UserIds);
        if (config.ToAdmins)
        {
            userIds.AddRange(await _databaseContext.Users.AsNoTracking()
                .Where(u => u.IsAdmin)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken));

            // root 账号通过 setting 表 root 键识别，可能未标记 IsAdmin
            var rootValue = await _databaseContext.Settings.AsNoTracking()
                .Where(s => s.Key == RootSettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (long.TryParse(rootValue, out var rootUserId))
            {
                userIds.Add(rootUserId);
            }
        }

        var recipients = userIds.Distinct().ToList();
        if (recipients.Count == 0)
        {
            throw new BusinessException("站内推送渠道未配置收件人") { StatusCode = 400 };
        }

        await _messageService.SendToUsersAsync(new MessageSendItem
        {
            Title = message.Title,
            Content = message.Content,
            MessageType = MessageType.System,
            Source = message.Source.Length == 0 ? "notification" : NotificationText.Truncate(message.Source, 50)
        }, recipients, cancellationToken);

        _logger.LogDebug("站内推送通知已发送：{ChannelName}，收件人 {Count}", channel.Name, recipients.Count);
    }
}
