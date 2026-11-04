using MoAI.App.Models;
using MoAI.Database.Entities;

namespace MoAI.App.Services;

/// <summary>
/// 外部 key 直连服务：把请求头中的应用接入 key（moai-ac-）或团队资源 key（moai-tr-）解析为外部 token 上下文，
/// 使 /api/teamres 团队资源接口免换取 token 直接访问；
/// 应用接入 key 团队级全量访问，资源 key 仅能访问绑定资源的端点；
/// 并为 key 直连维护「key 直连会话身份」外部用户，会话归属复用 CreateUserId = external_user.id 逻辑.
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
    /// 定位（必要时创建）key 直连会话身份：按（来源接入, 标识）定位，应用接入 key 用固定标识，
    /// 团队资源 key 用按 key 行 id 派生的标识（重置 key 后身份不变）.
    /// </summary>
    /// <param name="teamId">归属团队 id.</param>
    /// <param name="accessAppId">来源应用接入 id，资源 key 直连为 null.</param>
    /// <param name="appId">当前访问应用 id，会话面已知时回填授权应用.</param>
    /// <param name="externalUserIdMarker">直连身份标识，null 时用应用接入固定标识.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 key 直连身份对应的外部用户.</returns>
    Task<ExternalUserEntity> EnsurePrincipalUserAsync(int teamId, Guid? accessAppId, Guid? appId, string? externalUserIdMarker = null, CancellationToken cancellationToken = default);
}
