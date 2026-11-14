using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Models;
using MoAI.Auth.Queries;
using MoAI.Auth.Queries.Responses;
using MoAI.Auth.Services;
using MoAI.Database;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="QueryChallengePolicyQuery"/>.
/// 服务端实时判定当前客户端的验证要求：图形验证码按模式与失败/发码计数，邮箱验证码按策略 + SMTP 启用 + 账号已绑定邮箱.
/// </summary>
public class QueryChallengePolicyQueryHandler : IRequestHandler<QueryChallengePolicyQuery, QueryChallengePolicyQueryResponse>
{
    private readonly ILoginSecurityService _loginSecurityService;
    private readonly IRedisDatabase _redisDatabase;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryChallengePolicyQueryHandler"/> class.
    /// </summary>
    public QueryChallengePolicyQueryHandler(ILoginSecurityService loginSecurityService, IRedisDatabase redisDatabase, DatabaseContext databaseContext)
    {
        _loginSecurityService = loginSecurityService;
        _redisDatabase = redisDatabase;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryChallengePolicyQueryResponse> Handle(QueryChallengePolicyQuery request, CancellationToken cancellationToken)
    {
        var ip = request.ContextClientIp ?? string.Empty;
        var userName = request.UserName?.Trim() ?? string.Empty;

        var captchaRequired = await _loginSecurityService.ShouldRequireCaptchaAsync(request.Purpose, userName, ip, cancellationToken);

        var emailCodeRequired = false;
        if (request.Purpose == LoginCaptchaPurposes.Login)
        {
            if (string.IsNullOrEmpty(userName))
            {
                // 未输入用户名（页面挂载）时按模式给出答案，用户名失焦后会带 userName 重拉纠正
                emailCodeRequired = await _loginSecurityService.ShouldRequireLoginCodeAsync(0, cancellationToken);
            }
            else
            {
                var failCount = await _redisDatabase.GetAsync<int>($"login:fail:{userName}");
                if (await _loginSecurityService.ShouldRequireLoginCodeAsync(failCount, cancellationToken))
                {
                    // 与登录主链路同口径：账号未绑定邮箱（发不出码会锁死账号）或已绑定 TOTP（认证器优先）时不强制邮箱验证码
                    emailCodeRequired = await _databaseContext.Users
                        .AnyAsync(u => (u.UserName == userName || u.Email == userName)
                            && u.IsDeleted == 0 && !string.IsNullOrEmpty(u.Email) && !u.IsTotpEnabled, cancellationToken);
                }
            }
        }

        return new QueryChallengePolicyQueryResponse
        {
            CaptchaRequired = captchaRequired,
            EmailCodeRequired = emailCodeRequired
        };
    }
}
