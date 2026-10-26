using MoAI.App.Models;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用开通的外部渠道项.
/// </summary>
public class AppExternalChannelItem
{
    /// <summary>
    /// 渠道类型.
    /// </summary>
    public AppExternalChannelType ChannelType { get; set; }

    /// <summary>
    /// 打开渠道对话的链接（飞书为 applink 机器人单聊地址）.
    /// </summary>
    public string ChatUrl { get; set; } = default!;
}
