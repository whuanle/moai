using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.WeixinWork.Services;

/// <summary>
/// <inheritdoc cref="IWeixinWorkCallbackService"/>
/// </summary>
[InjectOnSingleton]
public sealed class WeixinWorkCallbackService : IWeixinWorkCallbackService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WeixinWorkEventForwarder _forwarder;
    private readonly ILogger<WeixinWorkCallbackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkCallbackService"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="forwarder">企业微信事件转发器.</param>
    /// <param name="logger">日志.</param>
    public WeixinWorkCallbackService(IServiceScopeFactory scopeFactory, WeixinWorkEventForwarder forwarder, ILogger<WeixinWorkCallbackService> logger)
    {
        _scopeFactory = scopeFactory;
        _forwarder = forwarder;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> VerifyAsync(Guid weixinWorkAppId, string msgSignature, string timestamp, string nonce, string echostr, CancellationToken cancellationToken = default)
    {
        var app = await GetAppAsync(weixinWorkAppId, cancellationToken);
        if (app == null)
        {
            throw new BusinessException("企业微信应用连接不存在.") { StatusCode = 404 };
        }

        if (!WeixinWorkMessageCrypt.VerifySignature(app.Token, timestamp, nonce, echostr, msgSignature))
        {
            throw new BusinessException("企业微信回调签名校验失败.") { StatusCode = 400 };
        }

        if (!WeixinWorkMessageCrypt.TryDecrypt(app.EncodingAesKey, echostr, app.CorpId, out var message, out _, out var error))
        {
            throw new BusinessException($"企业微信回调消息解密失败：{error}") { StatusCode = 400 };
        }

        return message;
    }

    /// <inheritdoc/>
    public async Task AcceptAsync(Guid weixinWorkAppId, string msgSignature, string timestamp, string nonce, string bodyXml, CancellationToken cancellationToken = default)
    {
        var app = await GetAppAsync(weixinWorkAppId, cancellationToken);
        if (app == null)
        {
            throw new BusinessException("企业微信应用连接不存在.") { StatusCode = 404 };
        }

        var envelope = WeixinWorkCallbackEnvelope.Parse(bodyXml);
        if (envelope == null || string.IsNullOrEmpty(envelope.Encrypt))
        {
            throw new BusinessException("企业微信回调报文格式不正确.") { StatusCode = 400 };
        }

        if (!WeixinWorkMessageCrypt.VerifySignature(app.Token, timestamp, nonce, envelope.Encrypt, msgSignature))
        {
            throw new BusinessException("企业微信回调签名校验失败.") { StatusCode = 400 };
        }

        if (!WeixinWorkMessageCrypt.TryDecrypt(app.EncodingAesKey, envelope.Encrypt, app.CorpId, out var messageXml, out _, out var error))
        {
            throw new BusinessException($"企业微信回调消息解密失败：{error}") { StatusCode = 400 };
        }

        // 已受理（应答 success），禁用的连接不再转发
        if (app.IsDisable)
        {
            _logger.LogDebug("企业微信应用已禁用，推送忽略，weixinWorkAppId={WeixinWorkAppId}.", weixinWorkAppId);
            return;
        }

        _forwarder.Forward(app.Id, messageXml);
    }

    private async Task<WeixinWorkAppEntity?> GetAppAsync(Guid weixinWorkAppId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await databaseContext.WeixinWorkApps
            .FirstOrDefaultAsync(x => x.Id == weixinWorkAppId, cancellationToken);
    }
}
