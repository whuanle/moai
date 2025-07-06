// <copyright file="DatabaseContext.Extensions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;
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
    /// <typeparam name="TSource"></typeparam>
    /// <param name="sources"></param>
    /// <returns></returns>
    public async Task<int> SoftDeleteAsync<TSource>(IQueryable<TSource> sources)
        where TSource : IDeleteAudited
    {
        var idProvider = _serviceProvider.GetRequiredService<IIdProvider>();

        return await sources.ExecuteUpdateAsync(x => x.SetProperty(a => a.IsDeleted, idProvider.NextId()));
    }
}