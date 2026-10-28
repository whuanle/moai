namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信回调地址（配置到企业微信管理端 接收消息 → URL），回调签名校验与推送转发统一入口：
/// <see cref="VerifyAsync"/> 处理企业微信 URL 有效性验证（GET echostr），<see cref="AcceptAsync"/> 处理消息推送（POST 加密报文），
/// 解密成功即代表已受理（HTTP 200 应答），后续转发给渠道处理器异步处理.
/// </summary>
public interface IWeixinWorkCallbackService
{
    /// <summary>
    /// 企业微信 URL 有效性验证：校验签名后解密 echostr，返回明文内容供原样回显.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id.</param>
    /// <param name="msgSignature">回调签名 msg_signature.</param>
    /// <param name="timestamp">时间戳.</param>
    /// <param name="nonce">随机数.</param>
    /// <param name="echostr">加密的验证串.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回解密后的明文 echostr.</returns>
    Task<string> VerifyAsync(Guid weixinWorkAppId, string msgSignature, string timestamp, string nonce, string echostr, CancellationToken cancellationToken = default);

    /// <summary>
    /// 接收消息推送：校验签名、解密报文后立即受理（转发器异步处理）；应用不存在/签名不符/解密失败抛出异常.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id.</param>
    /// <param name="msgSignature">回调签名 msg_signature.</param>
    /// <param name="timestamp">时间戳.</param>
    /// <param name="nonce">随机数.</param>
    /// <param name="bodyXml">推送报文原文（加密信封 XML）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回处理任务.</returns>
    Task AcceptAsync(Guid weixinWorkAppId, string msgSignature, string timestamp, string nonce, string bodyXml, CancellationToken cancellationToken = default);
}
