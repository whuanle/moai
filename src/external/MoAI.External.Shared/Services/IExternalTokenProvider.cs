using Microsoft.IdentityModel.Tokens;
using MoAI.Infra.Models;
using System.Security.Claims;

namespace MoAI.External.Services;

/// <summary>
/// 外部接入 Token 提供者.
/// </summary>
public interface IExternalTokenProvider
{
    /// <summary>
    /// 为外部应用生成 Token（2小时有效期）.
    /// </summary>
    /// <param name="appId">应用ID.</param>
    /// <param name="appName">应用名称.</param>
    /// <param name="teamId">所属团队ID.</param>
    /// <returns>AccessToken 和 RefreshToken.</returns>
    (string AccessToken, string RefreshToken) GenerateExternalAppTokens(Guid appId, string appName, int teamId);

    /// <summary>
    /// 为外部用户生成 Token（2小时有效期）.
    /// </summary>
    /// <param name="externalUserId">外部用户ID.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="nickName">昵称.</param>
    /// <param name="email">邮箱.</param>
    /// <param name="teamId">所属团队ID.</param>
    /// <returns>AccessToken 和 RefreshToken.</returns>
    (string AccessToken, string RefreshToken) GenerateExternalUserTokens(string externalUserId, string userName, string nickName, string email, int teamId);

    /// <summary>
    /// 验证 Token 是否有效.
    /// </summary>
    /// <param name="token">Token.</param>
    /// <returns>验证结果.</returns>
    Task<TokenValidationResult> ValidateTokenAsync(string token);

    /// <summary>
    /// 从 Token 解析用户上下文.
    /// </summary>
    /// <param name="token">Token.</param>
    /// <returns>用户上下文和 Claims.</returns>
    Task<(UserContext UserContext, IReadOnlyDictionary<string, Claim> Claims)> ParseUserContextFromTokenAsync(string token);
}
