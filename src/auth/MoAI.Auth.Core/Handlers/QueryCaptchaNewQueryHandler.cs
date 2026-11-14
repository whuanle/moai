using MediatR;
using MoAI.Auth.Queries;
using MoAI.Auth.Queries.Responses;
using MoAI.Auth.Services;
using MoAI.Infra.Models;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="QueryCaptchaNewQuery"/>.
/// 先计数再生成（20 次/分钟/IP），防止脚本刷图灌爆 Redis；超限 429 由前端提示稍后重试.
/// </summary>
public class QueryCaptchaNewQueryHandler : IRequestHandler<QueryCaptchaNewQuery, QueryCaptchaNewQueryResponse>
{
    private const int GenerateIpLimit = 20;

    private static readonly TimeSpan GenerateIpWindow = TimeSpan.FromMinutes(1);

    private readonly ICaptchaService _captchaService;
    private readonly IPublicAuthRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCaptchaNewQueryHandler"/> class.
    /// </summary>
    public QueryCaptchaNewQueryHandler(ICaptchaService captchaService, IPublicAuthRateLimiter rateLimiter)
    {
        _captchaService = captchaService;
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc/>
    public async Task<QueryCaptchaNewQueryResponse> Handle(QueryCaptchaNewQuery request, CancellationToken cancellationToken)
    {
        await _rateLimiter.HitAsync("captcha_new", request.ContextClientIp ?? string.Empty, GenerateIpLimit, GenerateIpWindow, cancellationToken);

        var captcha = await _captchaService.GenerateAsync(cancellationToken);

        return new QueryCaptchaNewQueryResponse
        {
            CaptchaId = captcha.CaptchaId,
            Image = captcha.Image,
            ImageMimeType = captcha.ImageMimeType,
            ExpiresInSeconds = captcha.ExpiresInSeconds
        };
    }
}
