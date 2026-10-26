using MoAI.Database.Enums;
using MoAI.DingTalk.Models;

namespace MoAI.DingTalk.Services;

/// <summary>
/// 钉钉事件处理器，业务模块（应用等）实现并注册，钉钉模块收到推送后按绑定关系转发：
/// 事件归属的钉钉应用绑定了哪个渠道，就投递给该渠道类型的全部处理器.
/// </summary>
public interface IDingTalkEventHandler
{
    /// <summary>
    /// 处理的渠道类型.
    /// </summary>
    DingTalkChannelType ChannelType { get; }

    /// <summary>
    /// 处理推送；处理器内部自行按 <see cref="DingTalkEventMessage.Topic"/> 过滤感兴趣的推送，实现方需自行捕获业务异常避免影响其它处理器.
    /// </summary>
    /// <param name="message">钉钉事件消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回处理任务.</returns>
    Task HandleAsync(DingTalkEventMessage message, CancellationToken cancellationToken);
}
