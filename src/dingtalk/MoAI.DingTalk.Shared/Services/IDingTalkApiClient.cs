namespace MoAI.DingTalk.Services;

/// <summary>
/// 钉钉机器人消息客户端，供业务模块在收到机器人消息后向会话回复.
/// </summary>
public interface IDingTalkApiClient
{
    /// <summary>
    /// 通过会话 webhook 发送文本消息（单聊/群聊通用，webhook 来自机器人消息推送体）.
    /// </summary>
    /// <param name="sessionWebhook">会话 webhook 地址（推送体 sessionWebhook 字段）.</param>
    /// <param name="text">文本内容.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回是否发送成功.</returns>
    Task<bool> SendTextMessageAsync(string sessionWebhook, string text, CancellationToken cancellationToken = default);
}
