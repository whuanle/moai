using System.Security.Claims;
using Maomi;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MoAI.App.Models;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Extensions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Services;

/// <summary>
/// <inheritdoc cref="IExternalTokenProvider"/>
/// </summary>
[InjectOnScoped]
public class ExternalTokenProvider : IExternalTokenProvider
{
#if DEBUG
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromDays(7);
#else
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(2);
#endif

    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IRsaProvider _rsaProvider;
    private readonly SystemOptions _systemOptions;
    private readonly System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler _tokenHandler = new() { MapInboundClaims = false };

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalTokenProvider"/> class.
    /// </summary>
    /// <param name="rsaProvider">RSA 签名密钥提供者.</param>
    /// <param name="systemOptions">系统配置.</param>
    public ExternalTokenProvider(IRsaProvider rsaProvider, SystemOptions systemOptions)
    {
        _rsaProvider = rsaProvider;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public (string AccessToken, string RefreshToken, int ExpiresIn) GenerateAppTokens(AccessAppEntity accessApp)
    {
        return GenerateTokens(
            UserType.ExternalApp,
            accessApp.Id.ToString(),
            accessApp.TeamId,
            accessApp.Name,
            accessApp.Id,
            appId: null,
            externalUserId: null,
            nickname: null);
    }

    /// <inheritdoc/>
    public (string AccessToken, string RefreshToken, int ExpiresIn) GenerateUserTokens(ExternalUserEntity external, AccessAppEntity? accessApp)
    {
        return GenerateTokens(
            UserType.External,
            external.Id.ToString(),
            external.TeamId,
            external.Nickname ?? external.ExternalUserId,
            external.AccessAppId,
            external.AppId,
            externalUserId: external.ExternalUserId,
            nickname: external.Nickname);
    }

    /// <inheritdoc/>
    public bool TryValidateAccessToken(string token, out ExternalTokenContext? context)
    {
        context = null;
        var principal = Validate(token, ExternalAuthDefaults.TokenTypeAccess);
        if (principal == null)
        {
            return false;
        }

        context = BuildContext(principal.Claims);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParseRefreshToken(string token, out ExternalTokenContext? context)
    {
        context = null;
        var principal = Validate(token, ExternalAuthDefaults.TokenTypeRefresh);
        if (principal == null)
        {
            return false;
        }

        context = BuildContext(principal.Claims);
        return true;
    }

    private static ExternalTokenContext BuildContext(IEnumerable<Claim> claims)
    {
        var claimMap = claims
            .GroupBy(x => x.Type, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal);

        var subjectType = claimMap.TryGetValue(JwtRegisteredClaimNames.Typ, out var typValue)
            ? typValue.JsonToObject<UserType>()
            : UserType.None;
        var subjectId = claimMap.GetValueOrDefault(JwtRegisteredClaimNames.Sub) ?? string.Empty;

        Guid? accessAppId = null;
        if (claimMap.TryGetValue(ExternalAuthDefaults.ClaimAccessAppId, out var accessAppIdValue) && Guid.TryParse(accessAppIdValue, out var accessAppIdGuid))
        {
            accessAppId = accessAppIdGuid;
        }

        Guid? appId = null;
        if (claimMap.TryGetValue(ExternalAuthDefaults.ClaimAppId, out var appIdValue) && Guid.TryParse(appIdValue, out var appIdGuid))
        {
            appId = appIdGuid;
        }

        long teamId = claimMap.TryGetValue(ExternalAuthDefaults.ClaimTeamId, out var teamIdValue) && long.TryParse(teamIdValue, out var teamIdParsed) ? teamIdParsed : 0;
        long externalId = subjectType == UserType.External && long.TryParse(subjectId, out var externalIdParsed) ? externalIdParsed : 0;

        return new ExternalTokenContext
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            ExternalId = externalId,
            TeamId = teamId,
            AccessAppId = accessAppId,
            AppId = appId,
            ExternalUserId = claimMap.GetValueOrDefault(ExternalAuthDefaults.ClaimExternalUserId),
            Nickname = claimMap.GetValueOrDefault(JwtRegisteredClaimNames.Nickname),
        };
    }

    private ClaimsPrincipal? Validate(string token, string expectedTokenType)
    {
        try
        {
            return _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = _systemOptions.Server,
                ValidateAudience = true,
                ValidAudience = ExternalAuthDefaults.BuildAudience(_systemOptions.Server),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _rsaProvider.GetRsaSecurityKey(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                // header typ 校验，access_token 与 refresh_token 不混用
                ValidTypes = new[] { expectedTokenType },
            }, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private (string AccessToken, string RefreshToken, int ExpiresIn) GenerateTokens(
        UserType subjectType,
        string subjectId,
        long teamId,
        string name,
        Guid? accessAppId,
        Guid? appId,
        string? externalUserId,
        string? nickname)
    {
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(_rsaProvider.GetPrivateRsa()), SecurityAlgorithms.RsaSha256);
        var audience = ExternalAuthDefaults.BuildAudience(_systemOptions.Server);

        var accessClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId),
            new(JwtRegisteredClaimNames.Name, name),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(JwtRegisteredClaimNames.Typ, subjectType.ToJsonString()),
            new(ExternalAuthDefaults.ClaimTeamId, teamId.ToString()),
            new(ExternalAuthDefaults.ClaimTokenType, ExternalAuthDefaults.TokenTypeAccess),
        };
        if (accessAppId != null)
        {
            accessClaims.Add(new Claim(ExternalAuthDefaults.ClaimAccessAppId, accessAppId.Value.ToString()));
        }

        if (appId != null)
        {
            accessClaims.Add(new Claim(ExternalAuthDefaults.ClaimAppId, appId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(externalUserId))
        {
            accessClaims.Add(new Claim(ExternalAuthDefaults.ClaimExternalUserId, externalUserId));
        }

        if (!string.IsNullOrEmpty(nickname))
        {
            accessClaims.Add(new Claim(JwtRegisteredClaimNames.Nickname, nickname));
        }

        var accessTokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(accessClaims),
            Issuer = _systemOptions.Server,
            Audience = audience,
            Expires = DateTime.Now.Add(AccessTokenLifetime),
            SigningCredentials = signingCredentials,
            TokenType = ExternalAuthDefaults.TokenTypeAccess,
        };
        var accessToken = _tokenHandler.CreateToken(accessTokenDescriptor);

        // refresh token 仅保留主体信息，授权在访问时按数据库团队级配置校验
        var refreshClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(JwtRegisteredClaimNames.Typ, subjectType.ToJsonString()),
            new(ExternalAuthDefaults.ClaimTokenType, ExternalAuthDefaults.TokenTypeRefresh),
        };
        var refreshTokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(refreshClaims),
            Issuer = _systemOptions.Server,
            Audience = audience,
            Expires = DateTime.Now.Add(RefreshTokenLifetime),
            SigningCredentials = signingCredentials,
            TokenType = ExternalAuthDefaults.TokenTypeRefresh,
        };
        var refreshToken = _tokenHandler.CreateToken(refreshTokenDescriptor);

        return (_tokenHandler.WriteToken(accessToken), _tokenHandler.WriteToken(refreshToken), (int)AccessTokenLifetime.TotalSeconds);
    }
}
