using Maomi;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MoAI.External.Services;
using MoAI.Infra;
using MoAI.Infra.Defaults;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Security.Claims;

namespace MoAI.External.Services;

/// <summary>
/// 外部接入 Token 提供者实现.
/// </summary>
[InjectOnScoped]
public class ExternalTokenProvider : IExternalTokenProvider
{
    private readonly IRsaProvider _rsaProvider;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// 外部接入 Token 有效期（小时）.
    /// </summary>
    private const int ExternalTokenExpirationHours = 2;

    /// <summary>
    /// 外部接入 RefreshToken 有效期（天）.
    /// </summary>
    private const int ExternalRefreshTokenExpirationDays = 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalTokenProvider"/> class.
    /// </summary>
    public ExternalTokenProvider(IRsaProvider rsaProvider, SystemOptions systemOptions)
    {
        _rsaProvider = rsaProvider;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public (string AccessToken, string RefreshToken) GenerateExternalAppTokens(Guid appId, string appName, int teamId)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, appId.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, appName),
            new Claim(JwtRegisteredClaimNames.Nickname, appName),
            new Claim(JwtRegisteredClaimNames.Email, string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, UserType.ExternalApp.ToJsonString()),
            new Claim("token_type", "access_token"),
            new Claim("team_id", teamId.ToString()),
        };

        return GenerateTokens(claims, appId.ToString());
    }

    /// <inheritdoc/>
    public (string AccessToken, string RefreshToken) GenerateExternalUserTokens(string externalUserId, string userName, string nickName, string email, int teamId)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, externalUserId),
            new Claim(JwtRegisteredClaimNames.Name, userName),
            new Claim(JwtRegisteredClaimNames.Nickname, nickName),
            new Claim(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, UserType.External.ToJsonString()),
            new Claim("token_type", "access_token"),
            new Claim("team_id", teamId.ToString()),
            new Claim("external_user_id", externalUserId),
        };

        return GenerateTokens(claims, externalUserId);
    }

    private (string AccessToken, string RefreshToken) GenerateTokens(List<Claim> claims, string subject)
    {
        var rsaKey = new RsaSecurityKey(_rsaProvider.GetPrivateRsa());

        // 生成 Access Token（2小时有效期）
        var accessTokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = claims.ToDictionary(x => x.Type, x => (object)x.Value),
            Subject = new ClaimsIdentity(claims),
            Issuer = _systemOptions.Server,
            Audience = _systemOptions.Server,
            Expires = DateTime.UtcNow.AddHours(ExternalTokenExpirationHours),
            SigningCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256),
            TokenType = "access_token",
        };

        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var accessToken = tokenHandler.CreateToken(accessTokenDescriptor);

        // 生成 Refresh Token
        var refreshTokenClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("token_type", "refresh_token"),
            new Claim(JwtRegisteredClaimNames.Typ, claims.First(c => c.Type == JwtRegisteredClaimNames.Typ).Value),
        };

        // 复制 team_id 到 refresh token
        var teamIdClaim = claims.FirstOrDefault(c => c.Type == "team_id");
        var refreshClaims = refreshTokenClaims.ToList();
        if (teamIdClaim != null)
        {
            refreshClaims.Add(new Claim("team_id", teamIdClaim.Value));
        }

        var refreshTokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = refreshClaims.ToDictionary(x => x.Type, x => (object)x.Value),
            Subject = new ClaimsIdentity(refreshClaims),
            Issuer = _systemOptions.Server,
            Audience = _systemOptions.Server,
            Expires = DateTime.UtcNow.AddDays(ExternalRefreshTokenExpirationDays),
            SigningCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256),
            TokenType = "refresh_token",
        };

        var refreshToken = tokenHandler.CreateToken(refreshTokenDescriptor);

        return (tokenHandler.WriteToken(accessToken), tokenHandler.WriteToken(refreshToken));
    }

    /// <inheritdoc/>
    public async Task<TokenValidationResult> ValidateTokenAsync(string token)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _systemOptions.Server,
            ValidateAudience = true,
            ValidAudience = _systemOptions.Server,
            ValidateLifetime = true,
            IssuerSigningKey = _rsaProvider.GetRsaSecurityKey(),
            ClockSkew = TimeSpan.Zero,
        };

        return await tokenHandler.ValidateTokenAsync(token, validationParameters);
    }

    /// <inheritdoc/>
    public async Task<(UserContext UserContext, IReadOnlyDictionary<string, Claim> Claims)> ParseUserContextFromTokenAsync(string token)
    {
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);

        var claims = jwtToken.Claims.ToDictionary(c => c.Type, c => c);

        var userTypeStr = claims.TryGetValue(JwtRegisteredClaimNames.Typ, out var typClaim) ? typClaim.Value : string.Empty;
        var userType = userTypeStr.JsonToObject<UserType>();

        var properties = new Dictionary<string, string>();
        if (claims.TryGetValue("team_id", out var teamIdClaim))
        {
            properties["team_id"] = teamIdClaim.Value;
        }

        if (claims.TryGetValue("external_user_id", out var externalUserIdClaim))
        {
            properties["external_user_id"] = externalUserIdClaim.Value;
        }

        var userContext = new DefaultUserContext
        {
            IsAuthenticated = true,
            UserType = userType,
            UserId = 0, // 外部用户没有内部用户ID
            UserName = claims.TryGetValue(JwtRegisteredClaimNames.Name, out var nameClaim) ? nameClaim.Value : string.Empty,
            NickName = claims.TryGetValue(JwtRegisteredClaimNames.Nickname, out var nickClaim) ? nickClaim.Value : string.Empty,
            Email = claims.TryGetValue(JwtRegisteredClaimNames.Email, out var emailClaim) ? emailClaim.Value : string.Empty,
            Properties = properties,
        };

        return await Task.FromResult((userContext as UserContext, claims as IReadOnlyDictionary<string, Claim>));
    }
}
