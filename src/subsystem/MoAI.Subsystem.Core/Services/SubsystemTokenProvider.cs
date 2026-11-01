using System.Security.Claims;
using Maomi;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Services;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// <inheritdoc cref="ISubsystemTokenProvider"/>
/// </summary>
[InjectOnScoped]
public class SubsystemTokenProvider : ISubsystemTokenProvider
{
#if DEBUG
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromDays(7);
#else
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(2);
#endif

    private static readonly TimeSpan UserAccessTokenLifetime = TimeSpan.FromHours(2);

    private static readonly TimeSpan UserRefreshTokenLifetime = TimeSpan.FromDays(7);

    private static readonly TimeSpan MachineRefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IRsaProvider _rsaProvider;
    private readonly SystemOptions _systemOptions;
    private readonly System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler _tokenHandler = new() { MapInboundClaims = false };

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemTokenProvider"/> class.
    /// </summary>
    /// <param name="rsaProvider">RSA 签名密钥提供者.</param>
    /// <param name="systemOptions">系统配置.</param>
    public SubsystemTokenProvider(IRsaProvider rsaProvider, SystemOptions systemOptions)
    {
        _rsaProvider = rsaProvider;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public bool TryValidateAccessToken(string token, out Guid subsysAppId, out string subsysAppKey)
    {
        subsysAppId = Guid.Empty;
        subsysAppKey = string.Empty;
        var claimMap = ValidateClaims(token, SubsystemAuthDefaults.TokenTypeAccess);
        if (claimMap == null)
        {
            return false;
        }

        return TryReadAppSubject(claimMap, out subsysAppId, out subsysAppKey);
    }

    /// <inheritdoc/>
    public (string AccessToken, string RefreshToken, int ExpiresIn) GenerateUserTokens(AccessAppEntity subsysApp, long userId)
    {
        var accessClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(SubsystemAuthDefaults.ClaimSubsystemId, subsysApp.Id.ToString()),
            new(SubsystemAuthDefaults.ClaimAppKey, subsysApp.Key),
            new(SubsystemAuthDefaults.ClaimTokenType, SubsystemAuthDefaults.TokenTypeUserAccess),
        };
        var accessToken = CreateToken(accessClaims, SubsystemAuthDefaults.TokenTypeUserAccess, UserAccessTokenLifetime);

        // refresh 仅保留主体信息，刷新时按数据库当前状态（接入启用/用户可用）重验
        var refreshClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(SubsystemAuthDefaults.ClaimSubsystemId, subsysApp.Id.ToString()),
            new(SubsystemAuthDefaults.ClaimTokenType, SubsystemAuthDefaults.TokenTypeUserRefresh),
        };
        var refreshToken = CreateToken(refreshClaims, SubsystemAuthDefaults.TokenTypeUserRefresh, UserRefreshTokenLifetime);

        return (accessToken, refreshToken, (int)UserAccessTokenLifetime.TotalSeconds);
    }

    /// <inheritdoc/>
    public bool TryValidateUserRefreshToken(string token, out Guid subsysAppId, out long userId)
    {
        subsysAppId = Guid.Empty;
        userId = 0;
        var claimMap = ValidateClaims(token, SubsystemAuthDefaults.TokenTypeUserRefresh);
        if (claimMap == null)
        {
            return false;
        }

        if (!claimMap.TryGetValue(SubsystemAuthDefaults.ClaimSubsystemId, out var sidValue) || !Guid.TryParse(sidValue, out subsysAppId))
        {
            return false;
        }

        return claimMap.TryGetValue(JwtRegisteredClaimNames.Sub, out var subValue) && long.TryParse(subValue, out userId);
    }

    /// <inheritdoc/>
    public (string AccessToken, string RefreshToken, int ExpiresIn) GenerateMachineTokens(AccessAppEntity subsysApp)
    {
        var accessClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subsysApp.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, subsysApp.Key),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(SubsystemAuthDefaults.ClaimAppKey, subsysApp.Key),
            new(SubsystemAuthDefaults.ClaimTokenType, SubsystemAuthDefaults.TokenTypeAccess),
        };
        var accessToken = CreateToken(accessClaims, SubsystemAuthDefaults.TokenTypeAccess, AccessTokenLifetime);

        // refresh 仅保留主体信息，刷新时按数据库当前状态（接入存在且启用）重验
        var refreshClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subsysApp.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(SubsystemAuthDefaults.ClaimAppKey, subsysApp.Key),
            new(SubsystemAuthDefaults.ClaimTokenType, SubsystemAuthDefaults.TokenTypeMachineRefresh),
        };
        var refreshToken = CreateToken(refreshClaims, SubsystemAuthDefaults.TokenTypeMachineRefresh, MachineRefreshTokenLifetime);

        return (accessToken, refreshToken, (int)AccessTokenLifetime.TotalSeconds);
    }

    /// <inheritdoc/>
    public bool TryValidateMachineRefreshToken(string token, out Guid subsysAppId, out string subsysAppKey)
    {
        subsysAppId = Guid.Empty;
        subsysAppKey = string.Empty;
        var claimMap = ValidateClaims(token, SubsystemAuthDefaults.TokenTypeMachineRefresh);
        if (claimMap == null)
        {
            return false;
        }

        return TryReadAppSubject(claimMap, out subsysAppId, out subsysAppKey);
    }

    private static bool TryReadAppSubject(IReadOnlyDictionary<string, string> claimMap, out Guid subsysAppId, out string subsysAppKey)
    {
        subsysAppKey = claimMap.GetValueOrDefault(SubsystemAuthDefaults.ClaimAppKey, string.Empty);
        if (claimMap.TryGetValue(JwtRegisteredClaimNames.Sub, out var subValue) && Guid.TryParse(subValue, out subsysAppId))
        {
            return true;
        }

        subsysAppId = Guid.Empty;
        return false;
    }

    private string CreateToken(List<Claim> claims, string tokenType, TimeSpan lifetime)
    {
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(_rsaProvider.GetPrivateRsa()), SecurityAlgorithms.RsaSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _systemOptions.Server,
            Audience = SubsystemAuthDefaults.BuildAudience(_systemOptions.Server),
            Expires = DateTime.Now.Add(lifetime),
            SigningCredentials = signingCredentials,

            // header typ 校验，与外部 token/内部 JWT 按 audience + 类型双重隔离
            TokenType = tokenType,
        };
        var token = _tokenHandler.CreateToken(descriptor);
        return _tokenHandler.WriteToken(token);
    }

    private IReadOnlyDictionary<string, string>? ValidateClaims(string token, string tokenType)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = _systemOptions.Server,
                ValidateAudience = true,
                ValidAudience = SubsystemAuthDefaults.BuildAudience(_systemOptions.Server),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _rsaProvider.GetRsaSecurityKey(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidTypes = new[] { tokenType },
            }, out _);

            return principal.Claims
                .GroupBy(x => x.Type, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal);
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
}
