using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.WeixinWork.Services;

namespace MoAI.WeixinWork.Controllers;

/// <summary>
/// 企业微信回调接入端点（匿名：由企业微信服务器调用）；每个企业微信应用连接一个回调地址，
/// 配置到管理端 接收消息 → URL，格式 {平台地址}/api/weixinwork_app/callback/{连接 id}。
/// GET 为 URL 有效性验证，POST 为消息推送；签名不符/解密失败返回错误，推送受理即回 success（业务异步处理）.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("/weixinwork_app/callback/{weixinWorkAppId:guid}")]
public class WeixinWorkCallbackController : ControllerBase
{
    private readonly IWeixinWorkCallbackService _callbackService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkCallbackController"/> class.
    /// </summary>
    /// <param name="callbackService">企业微信回调服务.</param>
    public WeixinWorkCallbackController(IWeixinWorkCallbackService callbackService)
    {
        _callbackService = callbackService;
    }

    /// <summary>
    /// 企业微信 URL 有效性验证：校验 msg_signature 后解密 echostr 原样回显明文.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id（路由）.</param>
    /// <param name="msgSignature">回调签名.</param>
    /// <param name="timestamp">时间戳.</param>
    /// <param name="nonce">随机数.</param>
    /// <param name="echostr">加密验证串.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回解密后的明文 echostr.</returns>
    [HttpGet]
    public async Task<IActionResult> Verify(
        [FromRoute] Guid weixinWorkAppId,
        [FromQuery(Name = "msg_signature")] string? msgSignature,
        [FromQuery] string? timestamp,
        [FromQuery] string? nonce,
        [FromQuery] string? echostr,
        CancellationToken cancellationToken)
    {
        var plain = await _callbackService.VerifyAsync(
            weixinWorkAppId,
            msgSignature ?? string.Empty,
            timestamp ?? string.Empty,
            nonce ?? string.Empty,
            echostr ?? string.Empty,
            cancellationToken);

        return Content(plain, "text/plain; charset=utf-8");
    }

    /// <summary>
    /// 接收企业微信消息推送：校验签名、解密信封并转发给渠道处理器（异步），立即应答 success.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id（路由）.</param>
    /// <param name="msgSignature">回调签名.</param>
    /// <param name="timestamp">时间戳.</param>
    /// <param name="nonce">随机数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回受理应答.</returns>
    [HttpPost]
    public async Task<IActionResult> Receive(
        [FromRoute] Guid weixinWorkAppId,
        [FromQuery(Name = "msg_signature")] string? msgSignature,
        [FromQuery] string? timestamp,
        [FromQuery] string? nonce,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        await _callbackService.AcceptAsync(
            weixinWorkAppId,
            msgSignature ?? string.Empty,
            timestamp ?? string.Empty,
            nonce ?? string.Empty,
            body,
            cancellationToken);

        // 明文 success 表示受理但不被动回复；回复消息走应用消息接口异步发送
        return Content("success", "text/plain; charset=utf-8");
    }
}
