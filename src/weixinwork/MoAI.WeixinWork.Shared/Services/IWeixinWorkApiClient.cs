namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信应用消息客户端，供业务模块在收到回调消息后向成员/群聊回复.
/// </summary>
public interface IWeixinWorkApiClient
{
    /// <summary>
    /// 发送文本消息：chatId 非空走应用推送群聊（appchat/send），否则走应用消息（message/send → toUserId 单聊）.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id.</param>
    /// <param name="toUserId">接收成员 userid，chatId 为空时必填.</param>
    /// <param name="chatId">群聊 id，非空时优先按群聊发送.</param>
    /// <param name="text">文本内容，超长按 UTF-8 字节截断（接口上限 2048 字节）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回是否发送成功.</returns>
    Task<bool> SendTextMessageAsync(Guid weixinWorkAppId, string toUserId, string chatId, string text, CancellationToken cancellationToken = default);
}
