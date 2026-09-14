using MoAI.Database.Enums;
using MoAI.Feishu.Models;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书事件处理器，业务模块（应用、知识库等）实现并注册，飞书模块收到事件后按绑定关系转发：
/// 事件归属的飞书应用绑定了哪个渠道，就投递给该渠道类型的全部处理器.
/// </summary>
public interface IFeishuEventHandler
{
    /// <summary>
    /// 处理的渠道类型.
    /// </summary>
    FeishuChannelType ChannelType { get; }

    /// <summary>
    /// 处理事件；处理器内部自行按 <see cref="FeishuEventMessage.EventType"/> 过滤感兴趣的事件，实现方需自行捕获业务异常避免影响其它处理器.
    /// </summary>
    /// <param name="message">飞书事件消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回处理任务.</returns>
    Task HandleAsync(FeishuEventMessage message, CancellationToken cancellationToken);
}
