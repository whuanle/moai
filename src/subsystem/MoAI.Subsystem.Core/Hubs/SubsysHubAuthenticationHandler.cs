using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Hubs;

/// <summary>
/// 机器端 Hub 认证：支持机器 token（typ=access_token）与 <c>moai-sa-</c> appid 直连，
/// 建连时回库校验系统级接入存在且启用.
/// </summary>
public class SubsysHubAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemTokenProvider _tokenProvider;
    private readonly ISubsystemAppIdCodec _appIdCodec;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysHubAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">认证选项.</param>
    /// <param name="logger">日志.</param>
    /// <param name="encoder">URL 编码器.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="tokenProvider">机器 token 校验器.</param>
    /// <param name="appIdCodec">结构化 appid 编解码.</param>
    public SubsysHubAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DatabaseContext databaseContext,
        ISubsystemTokenProvider tokenProvider,
        ISubsystemAppIdCodec appIdCodec)
        : base(options, logger, encoder)
    {
        _databaseContext = databaseContext;
        _tokenProvider = tokenProvider;
        _appIdCodec = appIdCodec;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("缺少接入凭据.");
        }

        AccessAppEntity? app = null;
        if (token.StartsWith(SubsystemAuthDefaults.KeyPrefixSystemApp, StringComparison.Ordinal))
        {
            if (_appIdCodec.TryDecode(token, out var appId))
            {
                app = await _databaseContext.AccessApps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Id == appId, Context.RequestAborted);
            }
            else if (token.Length == SubsystemAuthDefaults.LegacySystemAppKeyLength
                && SubsystemAuthDefaults.IsValidLegacySystemAppKey(token))
            {
                app = await _databaseContext.AccessApps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Key == token, Context.RequestAborted);
            }
        }
        else if (_tokenProvider.TryValidateAccessToken(token, out var tokenAppId, out var appKey))
        {
            app = await _databaseContext.AccessApps
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Id == tokenAppId, Context.RequestAborted);
            if (app != null && !string.Equals(app.Key, appKey, StringComparison.Ordinal))
            {
                app = null;
            }
        }

        if (app == null)
        {
            return AuthenticateResult.Fail("接入凭据无效.");
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            return AuthenticateResult.Fail("接入已停用.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, app.Id.ToString()),
            new Claim("sub", app.Id.ToString()),
            new Claim(SubsystemAuthDefaults.ClaimAppKey, app.Key),
            new Claim(SubsystemAuthDefaults.ClaimSubsystemId, app.Id.ToString()),
            new Claim(ClaimTypes.Name, app.Key),
            new Claim("apptype", ((int)app.AppType).ToString()),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private string? ReadToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var headerToken = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(headerToken))
            {
                return headerToken;
            }
        }

        var queryToken = Request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken;
    }
}
