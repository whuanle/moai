using Microsoft.EntityFrameworkCore;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;

namespace MoAI.App.Services;

/// <summary>
/// 应用外部渠道查询：从各平台应用连接的绑定关系（应用渠道独占）反查应用开通的外部渠道，
/// 并生成渠道对话入口链接（飞书为 applink 机器人单聊，钉钉为 jumprobot 机器人单聊）.
/// </summary>
internal static class AppExternalChannelHelper
{
    private const string DefaultFeishuApplinkBase = "https://applink.feishu.cn";

    /// <summary>
    /// 批量查询应用开通的外部渠道，返回 应用 id → 渠道列表；未开通渠道的应用不在字典中.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="appIds">应用 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public static async Task<Dictionary<Guid, List<AppExternalChannelItem>>> GetChannelsAsync(DatabaseContext databaseContext, IReadOnlyCollection<Guid> appIds, CancellationToken cancellationToken)
    {
        if (appIds.Count == 0)
        {
            return new Dictionary<Guid, List<AppExternalChannelItem>>();
        }

        var appIdSet = appIds.ToHashSet();
        var result = new Dictionary<Guid, List<AppExternalChannelItem>>();

        // 渠道 ChannelId 落库存的是创建绑定时的原始字符串，这里在内存解析为 Guid 再比对，避免大小写/格式差异漏配
        var feishuRows = await databaseContext.FeishuAppBindings
            .Where(x => x.ChannelType == (int)FeishuChannelType.App)
            .Join(
                databaseContext.FeishuApps,
                b => b.FeishuAppId,
                f => f.Id,
                (b, f) => new { b.ChannelId, f.AppId, f.Domain, f.IsDisable })
            .ToListAsync(cancellationToken);

        foreach (var row in feishuRows)
        {
            if (row.IsDisable || !Guid.TryParse(row.ChannelId, out var appId) || !appIdSet.Contains(appId))
            {
                continue;
            }

            Add(result, appId, new AppExternalChannelItem
            {
                ChannelType = AppExternalChannelType.Feishu,
                ChatUrl = BuildFeishuBotChatUrl(row.Domain, row.AppId),
            });
        }

        // 钉钉拉起机器人单聊的 dingtalkid 只能取回调 chatbotUserId（首条机器人消息后由事件转发器回填）；
        // 钉钉无按 AppKey 直接拉起机器人单聊的深链（jumprobot 传 AppKey 只会打开钉钉找不到机器人），
        // 未回填前不露出入口，回填后自动出现
        var dingtalkRows = await databaseContext.DingTalkAppBindings
            .Where(x => x.ChannelType == (int)DingTalkChannelType.App)
            .Join(
                databaseContext.DingTalkApps,
                b => b.DingTalkAppId,
                f => f.Id,
                (b, f) => new { b.ChannelId, f.IsDisable, f.ChatbotUserId })
            .ToListAsync(cancellationToken);

        foreach (var row in dingtalkRows)
        {
            if (row.IsDisable || string.IsNullOrEmpty(row.ChatbotUserId) || !Guid.TryParse(row.ChannelId, out var appId) || !appIdSet.Contains(appId))
            {
                continue;
            }

            Add(result, appId, new AppExternalChannelItem
            {
                ChannelType = AppExternalChannelType.Dingtalk,
                ChatUrl = $"dingtalk://dingtalkclient/action/jumprobot?dingtalkid={Uri.EscapeDataString(row.ChatbotUserId)}",
            });
        }

        return result;
    }

    private static void Add(Dictionary<Guid, List<AppExternalChannelItem>> result, Guid appId, AppExternalChannelItem item)
    {
        if (result.TryGetValue(appId, out var list))
        {
            list.Add(item);
        }
        else
        {
            result[appId] = new List<AppExternalChannelItem> { item };
        }
    }

    /// <summary>
    /// 飞书机器人单聊 applink：连接域名形如 https://open.feishu.cn / https://open.larksuite.com，
    /// applink 域名同构（applink.feishu.cn / applink.larksuite.com），未知域名回退飞书默认.
    /// </summary>
    private static string BuildFeishuBotChatUrl(string domain, string feishuAppId)
    {
        var applinkBase = domain.Contains("://open.") ? domain.Replace("://open.", "://applink.") : DefaultFeishuApplinkBase;
        return $"{applinkBase.TrimEnd('/')}/client/bot/open?appId={Uri.EscapeDataString(feishuAppId)}";
    }
}
