namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书租户访问凭证提供者，按飞书应用记录 id 获取 tenant_access_token 并缓存，供调用飞书开放平台 API 使用.
/// </summary>
public interface IFeishuAccessTokenProvider
{
    /// <summary>
    /// 获取飞书应用 tenant_access_token，命中缓存时不会发起网络请求.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id（feishu_app.id）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 tenant_access_token.</returns>
    Task<string> GetTenantAccessTokenAsync(Guid feishuAppId, CancellationToken cancellationToken = default);
}
