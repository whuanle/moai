using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Extensions;

namespace MoAI.AI.Services;

/// <summary>
/// 渠道用户匹配：把外部渠道消息的发送者平台标识（飞书 open_id/union_id、钉钉 unionId/userid、企微 userid）
/// 反查 <c>user_oauth_connection</c> 绑定记录，解析为 MoAI 内部用户.
/// <para>匹配键：候选标识命中绑定的 <c>sub</c> 或 <c>union_id</c> 均算命中（飞书 open_id 按应用隔离，
/// 跨应用场景靠 union_id 兜底）。命中多个不同用户、用户被禁用或未命中时返回空，调用方降级为匿名会话.</para>
/// </summary>
[InjectOnScoped]
public sealed class ChannelUserMatchService
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<ChannelUserMatchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelUserMatchService"/> class.
    /// </summary>
    public ChannelUserMatchService(DatabaseContext databaseContext, ILogger<ChannelUserMatchService> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <summary>
    /// 按平台类型与候选平台用户标识匹配内部用户；唯一命中且用户可用才返回，否则返回 null（匿名）.
    /// </summary>
    /// <param name="platform">OAuth 供应平台类型.</param>
    /// <param name="candidates">渠道消息里的发送者标识候选（如飞书 open_id + union_id），空值会被忽略.</param>
    /// <returns>命中的 MoAI 用户 id；未匹配返回 null.</returns>
    public async Task<long?> MatchAsync(OAuthPrivider platform, IReadOnlyList<string> candidates, CancellationToken cancellationToken = default)
    {
        var ids = candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return null;
        }

        // oauth_connection 是小表，provider 列为 JSON 字符串无法按枚举索引，载入后内存过滤
        var connections = await _databaseContext.OauthConnections.ToListAsync(cancellationToken);
        var providerIds = connections
            .Where(x => x.Provider.JsonToObject<OAuthPrivider>() == platform)
            .Select(x => x.Id)
            .ToArray();

        if (providerIds.Length == 0)
        {
            return null;
        }

        var userIds = await _databaseContext.UserOauthConnections
            .Where(x => providerIds.Contains(x.ProviderId)
                && (ids.Contains(x.Sub) || (x.UnionId != null && ids.Contains(x.UnionId))))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
        {
            return null;
        }

        if (userIds.Count > 1)
        {
            // 理论上仅同平台多连接器绑定到不同账号时可能出现，宁可不归属也不归错人
            _logger.LogWarning("渠道用户匹配命中多个用户，降级为匿名. platform={Platform} candidates={Candidates} userIds={UserIds}.", platform, ids, userIds);
            return null;
        }

        var userId = userIds[0];
        var user = await _databaseContext.Users
            .Where(x => x.Id == userId)
            .Select(x => new { x.IsDisable })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null || user.IsDisable)
        {
            _logger.LogInformation("渠道用户匹配命中但用户不可用，降级为匿名. platform={Platform} userId={UserId}.", platform, userId);
            return null;
        }

        return userId;
    }
}
