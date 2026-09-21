using System;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书长连接在线状态查询，由飞书模块（<c>FeishuConnectionManager</c>）实现，
/// 业务模块（知识库外部源等）只读消费，避免反向依赖飞书模块实现.
/// </summary>
public interface IFeishuConnectionStatus
{
    /// <summary>
    /// 指定飞书应用的长连接是否在线.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <returns>在线返回 true；未建立连接、已断开或应用不存在均返回 false.</returns>
    bool IsOnline(Guid feishuAppId);
}
