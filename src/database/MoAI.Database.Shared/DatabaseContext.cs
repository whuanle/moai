// <copyright file="MaomiaiContext.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Database.Audits;
using MoAI.Database.Entities;
using MoAI.Infra.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Models;
using System.Linq.Expressions;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext : DbContext
{
    protected readonly IServiceProvider _serviceProvider;
    protected readonly DatabaseOptions _contextOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="contextOptions"></param>
    public DatabaseContext(DbContextOptions options, IServiceProvider serviceProvider, DatabaseOptions contextOptions)
        : base(options)
    {
        _serviceProvider = serviceProvider;
        _contextOptions = contextOptions;

        // 配置过滤器.
        ChangeTracker.Tracked += (state, args) =>
        {
            AuditFilter(args);
        };

        ChangeTracker.StateChanged += (state, args) =>
        {
            AuditFilter(args);
        };
    }

    /// <summary>
    /// ai模型.
    /// </summary>
    public virtual DbSet<AiModelEntity> AiModels { get; set; }

    /// <summary>
    /// 文件列表.
    /// </summary>
    public virtual DbSet<FileEntity> Files { get; set; }

    /// <summary>
    /// 插件.
    /// </summary>
    public virtual DbSet<PluginEntity> Plugins { get; set; }

    /// <summary>
    /// 插件函数.
    /// </summary>
    public virtual DbSet<PluginFunctionEntity> PluginFunctions { get; set; }

    /// <summary>
    /// 系统设置.
    /// </summary>
    public virtual DbSet<SettingEntity> Settings { get; set; }

    /// <summary>
    /// 用户.
    /// </summary>
    public virtual DbSet<UserEntity> Users { get; set; }

    /// <summary>
    /// oauth2.0对接.
    /// </summary>
    public virtual DbSet<UserOauthEntity> UserOauths { get; set; }

    /// <summary>
    /// 知识库.
    /// </summary>
    public virtual DbSet<WikiEntity> Wikis { get; set; }

    /// <summary>
    /// 知识库配置.
    /// </summary>
    public virtual DbSet<WikiConfigEntity> WikiConfigs { get; set; }

    /// <summary>
    /// 知识库文档.
    /// </summary>
    public virtual DbSet<WikiDocumentEntity> WikiDocuments { get; set; }

    /// <summary>
    /// 知识库任务.
    /// </summary>
    public virtual DbSet<WikiDocumntTaskEntity> WikiDocumntTasks { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(_contextOptions.ConfigurationAssembly)
            .ApplyConfigurationsFromAssembly(_contextOptions.EntityAssembly);

        OnModelCreatingPartial(modelBuilder);
    }

    protected partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext
{
    protected partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        SeedData(modelBuilder);

        QueryFilter(modelBuilder);
    }

    protected static void QueryFilter(ModelBuilder modelBuilder)
    {
        // 给实体配置查询时自动加上 IsDeleted == false;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.IsAssignableTo(typeof(IDeleteAudited)))
            {
                // 构造 x => x.IsDeleted == 0
                var parameter = Expression.Parameter(entityType.ClrType, "x");
                MemberExpression property = Expression.Property(parameter, nameof(IDeleteAudited.IsDeleted));
                ConstantExpression constant = Expression.Constant(0L);
                BinaryExpression comparison = Expression.Equal(property, constant);

                var lambdaExpression = Expression.Lambda(comparison, parameter);

                entityType.SetQueryFilter(lambdaExpression);
            }
        }
    }

    // 定义种子数据
    protected void SeedData(ModelBuilder modelBuilder)
    {
        //modelBuilder.Entity<UserEntity>().HasData(
        //    new UserEntity
        //    {
        //    });
    }

    // 审计属性过滤
    protected void AuditFilter(EntityEntryEventArgs args)
    {
        var userContext = _serviceProvider.GetService<UserContext>();

        if (args.Entry.State == EntityState.Unchanged)
        {
            return;
        }

        if (args.Entry.State == EntityState.Added && args.Entry.Entity is ICreationAudited creationAudited)
        {
            creationAudited.CreateUserId = userContext?.UserId ?? default(int);
            creationAudited.CreateTime = DateTimeOffset.Now;
            if (args.Entry.Entity is IModificationAudited modificationAudited)
            {
                modificationAudited.UpdateUserId = userContext?.UserId ?? default(int);
                modificationAudited.UpdateTime = DateTimeOffset.Now;
            }
        }
        else if (args.Entry.State == EntityState.Modified && args.Entry.Entity is IModificationAudited modificationAudited)
        {
            modificationAudited.UpdateUserId = userContext?.UserId ?? default(int);
            modificationAudited.UpdateTime = DateTimeOffset.Now;
        }
        else if (args.Entry.State == EntityState.Deleted && args.Entry.Entity is IDeleteAudited deleteAudited)
        {
            args.Entry.State = EntityState.Modified;

            deleteAudited.IsDeleted = DateTimeOffset.Now.Ticks;
            deleteAudited.UpdateUserId = userContext?.UserId ?? default(int);
            deleteAudited.UpdateTime = DateTimeOffset.Now;
        }
    }
}