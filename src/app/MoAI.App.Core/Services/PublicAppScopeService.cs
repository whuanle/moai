using Maomi;
using MoAI.Database.Enums;
using MoAI.Org.Services;

namespace MoAI.App.Services;

/// <summary>
/// 应用市场上架可视范围判定：公开应用（is_public）的可视范围存于统一组织授权（publicApp 资源类型），
/// 无授权记录 = 全员可见（存量已上架应用天然兼容）；有记录时仅被范围组织覆盖的用户放行.
/// </summary>
public interface IPublicAppScopeService
{
    /// <summary>
    /// 判定用户是否在应用可视范围内（应用广场可见性与公开应用使用门禁共用）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否放行.</returns>
    Task<bool> IsAllowedAsync(Guid appId, long userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="IPublicAppScopeService"/>
/// </summary>
[InjectOnScoped]
public class PublicAppScopeService : IPublicAppScopeService
{
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicAppScopeService"/> class.
    /// </summary>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public PublicAppScopeService(IOrgGrantService orgGrantService)
    {
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAllowedAsync(Guid appId, long userId, CancellationToken cancellationToken = default)
    {
        var grants = await _orgGrantService.GetGrantsAsync(OrgGrantResourceType.PublicApp, appId, cancellationToken);
        if (grants.Count == 0)
        {
            // 无授权记录 = 全员可见
            return true;
        }

        return await _orgGrantService.IsAllowedAsync(OrgGrantResourceType.PublicApp, appId, userId, cancellationToken);
    }
}
