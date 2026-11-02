using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Extensions;

namespace MoAI.Notification.Services;

/// <summary>
/// 应用机器人渠道的投递目标：一个站内用户在第三方平台上的标识.
/// </summary>
/// <param name="UserId">站内用户 id.</param>
/// <param name="Sub">第三方登录唯一标识（飞书 open_id / 钉钉 unionId / 企业微信 userid）.</param>
/// <param name="UnionId">飞书 union_id（跨应用一致，仅飞书绑定有值）.</param>
public record NotificationPlatformUser(long UserId, string Sub, string? UnionId);

/// <summary>
/// 站内用户 → 第三方平台标识解析：按渠道类型反查 oauth_connection（provider）与 user_oauth_connection（绑定），
/// 供飞书/钉钉/企业微信应用机器人渠道把「站内用户 id」换算为「平台侧用户 id」后单聊投递.
/// </summary>
/// <remarks>
/// 渠道配置的应用凭证与第三方登录的连接相互独立（机器人不等于登录应用）；平台标识取自登录时的绑定记录，
/// 跨应用可用性由平台侧语义保证：飞书 union_id 同主体一致、钉钉 unionId 由通知应用换算企业内 userid、企业微信 userid 企业内一致.
/// </remarks>
[InjectOnScoped]
public class NotificationPlatformUserResolver
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationPlatformUserResolver"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public NotificationPlatformUserResolver(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 解析投递目标：全部（或指定站内用户中）绑定了该平台账号的启用用户，同一用户多次绑定取最新一条.
    /// </summary>
    /// <param name="channelType">应用机器人渠道类型（仅 FeishuAppBot/DingtalkAppBot/WecomAppBot 有意义）.</param>
    /// <param name="targetUserIds">限定站内用户 id 集合，空表示全部绑定用户.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回去重后的平台标识列表，无任何匹配时为空列表.</returns>
    public async Task<IReadOnlyList<NotificationPlatformUser>> ResolveAsync(NotificationChannelType channelType, IReadOnlyList<long>? targetUserIds, CancellationToken cancellationToken)
    {
        var provider = channelType switch
        {
            NotificationChannelType.FeishuAppBot => OAuthPrivider.Feishu,
            NotificationChannelType.DingtalkAppBot => OAuthPrivider.DingTalk,
            NotificationChannelType.WecomAppBot => OAuthPrivider.WeixinWork,
            _ => default(OAuthPrivider?),
        };

        if (provider == null)
        {
            return Array.Empty<NotificationPlatformUser>();
        }

        // oauth_connection.provider 存的是枚举 JSON 字符串（如 "feishu"），与创建连接时 ToJsonString 写入口径一致
        var providerJson = TextToJsonExtensions.ToJsonString(provider.Value);
        var providerIds = await _databaseContext.OauthConnections.AsNoTracking()
            .Where(x => x.Provider == providerJson)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (providerIds.Count == 0)
        {
            return Array.Empty<NotificationPlatformUser>();
        }

        var query = from binding in _databaseContext.UserOauthConnections.AsNoTracking()
                    join user in _databaseContext.Users.AsNoTracking() on binding.UserId equals user.Id
                    where providerIds.Contains(binding.ProviderId) && !user.IsDisable
                    select binding;
        if (targetUserIds is { Count: > 0 })
        {
            var userIds = targetUserIds.ToList();
            query = query.Where(x => userIds.Contains(x.UserId));
        }

        var bindings = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        return bindings
            .GroupBy(x => x.UserId)
            .Select(x => x.Last())
            .Select(x => new NotificationPlatformUser(x.UserId, x.Sub, x.UnionId))
            .ToList();
    }
}
