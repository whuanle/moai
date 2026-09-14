using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App;

/// <summary>
/// 外部 token 认证方案：校验 /api/external 专用的 access token（audience = Server + |external），
/// 与内部 JWT（audience = Server）互相隔离，外部 token 无法访问内部接口，内部 token 也无法访问外部接口.
/// </summary>
public class ExternalJwtBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IExternalTokenProvider _externalTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalJwtBearerAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">方案选项.</param>
    /// <param name="logger">日志工厂.</param>
    /// <param name="encoder">URL 编码器.</param>
    /// <param name="externalTokenProvider">外部 token 服务.</param>
    public ExternalJwtBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IExternalTokenProvider externalTokenProvider)
        : base(options, logger, encoder)
    {
        _externalTokenProvider = externalTokenProvider;
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadBearerToken();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!_externalTokenProvider.TryValidateAccessToken(token, out var tokenContext) || tokenContext == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid external token."));
        }

        Context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;

        var claims = new List<Claim>
        {
            new("subject_type", tokenContext.SubjectType.ToString()),
            new("subject_id", tokenContext.SubjectId),
            new(ClaimTypes.Name, tokenContext.Nickname ?? tokenContext.SubjectId),
        };

        // 携带内部解析所需的 claims：用户 token 的 external_user.id 落入 NameIdentifier（UserContextProvider 可解析，
        // AppAgentDispatcher 等内部组件据此校验会话归属）；应用 token 无数值主体，固定 0（匿名语义）
        if (tokenContext.SubjectType == UserType.External)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, tokenContext.ExternalId.ToString()));
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "0"));
        }

        claims.Add(new Claim(JwtRegisteredClaimNames.Typ, tokenContext.SubjectType.ToJsonString()));
        if (!string.IsNullOrEmpty(tokenContext.Nickname))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nickname, tokenContext.Nickname));
        }

        var identity = new ClaimsIdentity(claims, ExternalAuthDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ExternalAuthDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":{\"message\":\"Invalid or missing external token.\",\"type\":\"authentication_error\",\"code\":\"invalid_token\"}}");
    }

    /// <inheritdoc/>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":{\"message\":\"You do not have access to this resource.\",\"type\":\"permission_error\",\"code\":\"permission_denied\"}}");
    }

    private string? ReadBearerToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
