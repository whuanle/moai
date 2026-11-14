using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Message.Models;
using MoAI.Message.Services;
using MoAI.Team.Services;

namespace MoAI.Message.Services;

/// <summary>
/// 站内信领域服务实现：逐收件人落库（已读态/软删独立），成功后触发 message.new 事件异步推送.
/// </summary>
[InjectOnScoped]
public class MessageService : IMessageService
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IEventPushService _eventPushService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="eventPushService">事件推送领域服务.</param>
    public MessageService(DatabaseContext databaseContext, ITeamService teamService, IEventPushService eventPushService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _eventPushService = eventPushService;
    }

    /// <inheritdoc/>
    public async Task SendToUsersAsync(MessageSendItem message, IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default)
    {
        ValidateContent(message);

        var recipientIds = userIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (recipientIds.Length == 0)
        {
            throw new BusinessException("站内信收件人不能为空.") { StatusCode = 400 };
        }

        await SaveAndPushAsync(message, recipientIds, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendToTeamAsync(MessageSendItem message, long teamId, CancellationToken cancellationToken = default)
    {
        ValidateContent(message);

        var teamExists = await _databaseContext.Teams
            .AnyAsync(x => x.Id == teamId, cancellationToken);
        if (!teamExists)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        var memberIds = await _teamService.GetMemberUserIdsAsync(teamId, cancellationToken);
        if (memberIds.Count == 0)
        {
            return;
        }

        await SaveAndPushAsync(message, memberIds, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendToAllUsersAsync(MessageSendItem message, CancellationToken cancellationToken = default)
    {
        ValidateContent(message);

        var userIds = await _databaseContext.Users
            .Where(x => !x.IsDisable)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        if (userIds.Length == 0)
        {
            return;
        }

        await SaveAndPushAsync(message, userIds, cancellationToken);
    }

    private async Task SaveAndPushAsync(MessageSendItem message, IReadOnlyCollection<long> recipientIds, CancellationToken cancellationToken)
    {
        var entities = recipientIds.Select(userId => new MessageEntity
        {
            RecipientUserId = userId,
            Title = message.Title,
            Content = message.Content,
            MessageType = (int)message.MessageType,
            Source = message.Source ?? string.Empty,
            SenderUserId = message.SenderUserId,
            Params = message.Params,
            IsRead = false
        }).ToList();

        _databaseContext.Messages.AddRange(entities);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 落库成功后异步推送事件（只推事件不推数据），离线用户由上线后的接口拉取兜底
        await _eventPushService.PushToUsersAsync(
            recipientIds,
            EventNames.MessageNew,
            new Dictionary<string, string>
            {
                ["source"] = message.Source ?? string.Empty,
                ["messageType"] = message.MessageType.ToString().ToLowerInvariant()
            },
            cancellationToken);
    }

    private static void ValidateContent(MessageSendItem message)
    {
        if (string.IsNullOrWhiteSpace(message.Title))
        {
            throw new BusinessException("站内信标题不能为空.") { StatusCode = 400 };
        }

        if (message.Title.Length > 256)
        {
            throw new BusinessException("站内信标题最长 256 个字符.") { StatusCode = 400 };
        }

        if (string.IsNullOrWhiteSpace(message.Content))
        {
            throw new BusinessException("站内信正文不能为空.") { StatusCode = 400 };
        }

        if (message.Content.Length > 4000)
        {
            throw new BusinessException("站内信正文最长 4000 个字符.") { StatusCode = 400 };
        }
    }
}
