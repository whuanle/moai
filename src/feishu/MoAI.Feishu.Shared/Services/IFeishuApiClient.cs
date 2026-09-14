namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书开放平台 API 客户端，供业务模块在收到事件后回调飞书（如回复群聊消息）.
/// </summary>
public interface IFeishuApiClient
{
    /// <summary>
    /// 发送文本消息，使用该飞书应用的身份发送.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="receiveIdType">接收者类型：open_id/user_id/union_id/email/chat_id.</param>
    /// <param name="receiveId">接收者 id，群聊为 chat_id.</param>
    /// <param name="text">文本内容.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回飞书消息 id（om_xxx）.</returns>
    Task<string> SendTextMessageAsync(Guid feishuAppId, string receiveIdType, string receiveId, string text, CancellationToken cancellationToken = default);
}
