using Microsoft.IdentityModel.Tokens;
using MoAI.Infra.Models;
using System.Security.Claims;

namespace MoAI.Auth.Services;

/// <summary>
/// 构建 token.
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// 生成 JWT Access Token 和 Refresh Token.
    /// </summary>
    /// <param name="userContext">用户上下文.</param>
    /// <param name="authVersion">用户当前鉴权版本（写入 token，供每请求吊销比对）.</param>
    /// <returns>token.</returns>
    (string AccessToken, string RefreshToken) GenerateTokens(UserContext userContext, long authVersion = 0);

    /// <summary>
    /// 从 token 解析用户信息.
    /// </summary>
    /// <param name="token"></param>
    /// <returns>用户信息.</returns>
    Task<(UserContext UserContext, IReadOnlyDictionary<string, Claim> Claims)> ParseUserContextFromTokenAsync(string token);

    /// <summary>
    /// 验证token 是否有效，AccessToken 或 RefreshToken.
    /// </summary>
    /// <param name="token"></param>
    /// <returns>检验结果.</returns>
    Task<TokenValidationResult> ValidateTokenAsync(string token);
}