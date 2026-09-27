using MoAI.App.Models;
using MoAI.Database.Entities;

namespace MoAI.App.Services;

/// <summary>
/// 外部 key 直连服务：把请求头中的应用接入 key（moai-ac-）或团队接入 key（moai-）解析为外部 token 上下文，
/// 使 /api/external 团队资源接口免换取 token 直接访问（范围以 key 勾选为准）；
/// 并为应用接入 key 维护「key 直连会话身份」外部用户，会话归属复用 CreateUserId = external_user.id 逻辑.
/// </summary>
public interface IExternalAccessKeyService
{
    /// <summary>
    /// 解析 key 直连请求上下文；key 无效返回 null（调用方按 401 处理）.
    /// </summary>
    /// <param name="apiKey">key 原文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回外部 token 上下文，key 无效为 null.</returns>
    Task<ExternalTokenContext?> ResolveContextAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 定位（必要时创建）key 直连会话身份：按（来源应用接入, 固定标识）唯一；
    /// 仅应用接入 key 走此语义，团队接入 key 直连不允许发起会话.
    /// </summary>
    /// <param name="teamId">归属团队 id.</param>
    /// <param name="accessAppId">来源应用接入 id.</param>
    /// <param name="appId">当前访问应用 id，会话面已知时回填授权应用.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 key 直连身份对应的外部用户.</returns>
    Task<ExternalUserEntity> EnsurePrincipalUserAsync(int teamId, Guid accessAppId, Guid? appId, CancellationToken cancellationToken = default);
}
