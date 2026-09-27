using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.App.Services;

/// <summary>
/// <inheritdoc cref="IExternalAccessKeyService"/>
/// </summary>
[InjectOnScoped]
public class ExternalAccessKeyService : IExternalAccessKeyService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalAccessKeyService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="scopeFactory">DI 作用域工厂（直连身份落库须用独立作用域，避免触发请求级用户上下文缓存）.</param>
    public ExternalAccessKeyService(DatabaseContext databaseContext, IServiceScopeFactory scopeFactory)
    {
        _databaseContext = databaseContext;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 解析 key 直连请求上下文；key 无效返回 null（调用方按 401 处理）.
    /// 应用接入 key 沿用既有语义（存在即有效，软删除自动过滤）.
    /// </summary>
    /// <param name="apiKey">key 原文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回外部 token 上下文，key 无效为 null.</returns>
    public async Task<ExternalTokenContext?> ResolveContextAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (apiKey.StartsWith(ExternalAuthDefaults.AccessAppKeyPrefix, StringComparison.Ordinal))
        {
            var accessApp = await _databaseContext.AccessApps
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == apiKey, cancellationToken);
            if (accessApp == null)
            {
                return null;
            }

            return new ExternalTokenContext
            {
                SubjectType = UserType.ExternalApp,
                SubjectId = accessApp.Id.ToString(),
                ExternalId = 0,
                TeamId = accessApp.TeamId,
                AccessAppId = accessApp.Id,
                Scopes = ToWikiScopes(accessApp.Scopes),
                IsKeyDirect = true,
            };
        }

        return null;
    }

    /// <summary>
    /// 定位（必要时创建）key 直连会话身份：按（来源应用接入, 固定标识）唯一，
    /// 应用接入 key 直连发起会话/对话时复用为 external_user 记录.
    /// </summary>
    /// <param name="teamId">归属团队 id.</param>
    /// <param name="accessAppId">来源应用接入 id.</param>
    /// <param name="appId">当前访问应用 id，会话面已知时回填授权应用.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 key 直连身份对应的外部用户.</returns>
    public async Task<ExternalUserEntity> EnsurePrincipalUserAsync(int teamId, Guid accessAppId, Guid? appId, CancellationToken cancellationToken = default)
    {
        // 独立子作用域落库：SaveChanges 的审计拦截会触发本请求 IUserContextProvider 的懒加载用户上下文，
        // 此时直连身份尚未写入 NameIdentifier，请求级缓存会被 0 污染（会话 create_user_id 归属错误）；
        // 子作用域内的提供者实例与请求级相互隔离
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var marker = ExternalAuthDefaults.KeyDirectExternalUserId;
        var external = await databaseContext.ExternalUsers
            .FirstOrDefaultAsync(x => x.AccessAppId == accessAppId && x.ExternalUserId == marker, cancellationToken);

        if (external == null)
        {
            external = new ExternalUserEntity
            {
                TeamId = teamId,
                AccessAppId = accessAppId,
                ExternalUserId = marker,
            };
            databaseContext.ExternalUsers.Add(external);
        }

        if (appId != null)
        {
            external.AppId = appId;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        return external;
    }

    /// <summary>
    /// key 直连的资源范围只保留知识库维度（model/external_token 是 key 层概念，不进入外部资源范围）.
    /// </summary>
    private static TeamApiKeyScopes ToWikiScopes(int scopes)
    {
        return (TeamApiKeyScopes)scopes & TeamApiKeyScopeCodes.ExternalDimensions;
    }
}
