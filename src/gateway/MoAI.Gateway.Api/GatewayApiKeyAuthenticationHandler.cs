using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;

namespace MoAI.Gateway;

/// <summary>
/// 网关 API Key 认证：支持 Authorization: Bearer 与 x-api-key 两种携带方式，
/// 仅接受应用接入 key（moai-ac-，access_app，团队网关页不再提供密钥管理），
/// 是否可用模型渠道由该接入勾选的 model 范围在端点层判定.
/// </summary>
public class GatewayApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayApiKeyAuthenticationHandler"/> class.
    /// </summary>
    public GatewayApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DatabaseContext databaseContext)
        : base(options, logger, encoder)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = ReadApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        if (!apiKey.StartsWith(GatewayApiKeyDefaults.AccessAppKeyPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        // 应用接入 key（moai-ac-）认证：key 明文比对 access_app.key；可用性沿用应用接入既有语义（存在即有效，软删除自动过滤）
        var accessApp = await _databaseContext.AccessApps.FirstOrDefaultAsync(x => x.Key == apiKey);
        if (accessApp == null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, accessApp.CreateUserId.ToString()),
            new(ClaimTypes.Name, accessApp.Name),
            new("teamid", accessApp.TeamId.ToString()),
            new("keyid", accessApp.Id.ToString()),
            new(GatewayApiKeyDefaults.ClaimScopes, TeamApiKeyScopeCodes.ToClaimValue((TeamApiKeyScopes)accessApp.Scopes)),
        };

        var identity = new ClaimsIdentity(claims, GatewayApiKeyDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), GatewayApiKeyDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":{\"message\":\"Invalid or missing API key.\",\"type\":\"authentication_error\",\"code\":\"invalid_api_key\"}}");
    }

    /// <inheritdoc/>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":{\"message\":\"You do not have access to this resource.\",\"type\":\"permission_error\",\"code\":\"permission_denied\"}}");
    }

    private string? ReadApiKey()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
        }

        var headerKey = Request.Headers["x-api-key"].ToString();
        return string.IsNullOrEmpty(headerKey) ? null : headerKey.Trim();
    }
}
