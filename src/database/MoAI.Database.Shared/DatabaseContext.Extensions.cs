using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database.Audits;
using MoAI.Infra.Services;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext
{
    /// <summary>
    /// 软删除.
    /// </summary>
    /// <typeparam name="TSource">数据源.</typeparam>
    /// <param name="sources"></param>
    /// <returns></returns>
    public async Task<int> SoftDeleteAsync<TSource>(IQueryable<TSource> sources)
        where TSource : IDeleteAudited
    {
        var idProvider = _serviceProvider.GetRequiredService<IIdProvider>();

        return await sources.ExecuteUpdateAsync(x => x.SetProperty(a => a.IsDeleted, idProvider.NextId()));
    }

    /// <summary>
    /// 批量更新.
    /// </summary>
    /// <typeparam name="TSource">数据源.</typeparam>
    /// <param name="source"></param>
    /// <param name="setPropertyCalls"></param>
    /// <returns></returns>
    public async Task<int> WhereUpdateAsync<TSource>(
        IQueryable<TSource> source,
        Action<UpdateSettersBuilder<TSource>> setPropertyCalls)
        where TSource : IModificationAudited
    {
        var userProvider = _serviceProvider.GetRequiredService<IUserContextProvider>();
        var userContext = userProvider.GetUserContext();

        Action<UpdateSettersBuilder<TSource>> auditSetters =
            x => x.SetProperty(a => a.UpdateUserId, userContext.UserId)
                  .SetProperty(a => a.UpdateTime, DateTimeOffset.Now);

        var combinedSetters = new Action<UpdateSettersBuilder<TSource>>(setter =>
        {
            setPropertyCalls(setter);
            auditSetters(setter);
        });

        return await source.ExecuteUpdateAsync(combinedSetters);
    }
}