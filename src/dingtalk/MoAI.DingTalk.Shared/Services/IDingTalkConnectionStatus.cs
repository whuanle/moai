using System;

namespace MoAI.DingTalk.Services;

/// <summary>
/// 钉钉长连接在线状态查询，由钉钉模块（<c>DingTalkConnectionManager</c>）实现，业务模块只读消费，避免反向依赖钉钉模块实现.
/// </summary>
public interface IDingTalkConnectionStatus
{
    /// <summary>
    /// 指定钉钉应用的长连接是否在线.
    /// </summary>
    /// <param name="dingTalkAppId">钉钉应用记录 id.</param>
    /// <returns>在线返回 true；未建立连接、已断开或应用不存在均返回 false.</returns>
    bool IsOnline(Guid dingTalkAppId);
}
