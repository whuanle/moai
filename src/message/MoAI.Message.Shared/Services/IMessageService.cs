using MoAI.Message.Models;

namespace MoAI.Message.Services;

/// <summary>
/// 站内信领域服务：供其它模块发送站内信（落库 + 异步事件推送），收件人在发送时解析为具体用户.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// 发送站内信给指定用户集合（1..N），逐收件人落库并触发 message.new 事件推送.
    /// </summary>
    /// <param name="message">消息内容.</param>
    /// <param name="userIds">收件人用户 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SendToUsersAsync(MessageSendItem message, IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送站内信给团队全部成员，团队不存在时抛出 BusinessException.
    /// </summary>
    /// <param name="message">消息内容.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SendToTeamAsync(MessageSendItem message, long teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送站内信给平台全部启用用户（广播）.
    /// </summary>
    /// <param name="message">消息内容.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SendToAllUsersAsync(MessageSendItem message, CancellationToken cancellationToken = default);
}
