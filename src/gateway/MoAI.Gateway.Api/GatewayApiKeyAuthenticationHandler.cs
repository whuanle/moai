using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Gateway.Services;
using MoAI.Team.Services;

namespace MoAI.Gateway;

/// <summary>
/// 网关 API Key 认证：支持 Authorization: Bearer 与 x-api-key 两种携带方式，
/// 密钥须启用、未过期、创建者未禁用且创建者仍是团队成员.
/// </summary>
public class GatewayApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayApiKeyAuthenticationHandler"/> class.
    /// </summary>
    public GatewayApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserAccountService userAccountService)
        : base(options, logger, encoder)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = ReadApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        if (!apiKey.StartsWith("moai-", StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var sha256 = ApiKeyGenerator.Hash(apiKey);
        var entity = await _databaseContext.TeamApiKeys.FirstOrDefaultAsync(x => x.KeySha256 == sha256);
        if (entity == null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (entity.IsDisable)
        {
            return AuthenticateResult.Fail("API key is disabled.");
        }

        if (entity.ExpireTime < DateTimeOffset.Now)
        {
            return AuthenticateResult.Fail("API key is expired.");
        }

        // 创建者被禁用/删除时密钥同步失效（替代全局中间件的用户状态检查）.
        var creatorState = await _userAccountService.GetUserStateAsync(entity.CreatorUserId, Context.RequestAborted);
        if (creatorState.IsDeleted || creatorState.IsDisable)
        {
            return AuthenticateResult.Fail("API key is unavailable.");
        }

        // 创建者必须仍是团队成员，移出团队即吊销其创建的密钥.
        var role = await _teamService.GetMyRoleAsync(entity.TeamId, entity.CreatorUserId);
        if (role == null)
        {
            return AuthenticateResult.Fail("API key is unavailable.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, entity.CreatorUserId.ToString()),
            new(ClaimTypes.Name, entity.Name),
            new("teamid", entity.TeamId.ToString()),
            new("keyid", entity.Id.ToString()),
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
