// <copyright file="DatabaseContext.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext : DbContext
{
    protected readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    public DatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
        : base(options)
    {
        _serviceProvider = serviceProvider;

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
    /// oauth2.0系统.
    /// </summary>
    public virtual DbSet<OauthConnectionEntity> OauthConnections { get; set; }

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
    /// 知识库文档.
    /// </summary>
    public virtual DbSet<WikiDocumentEntity> WikiDocuments { get; set; }

    /// <summary>
    /// 知识库任务.
    /// </summary>
    public virtual DbSet<WikiDocumentTaskEntity> WikiDocumentTasks { get; set; }

    /// <summary>
    /// 知识库成员.
    /// </summary>
    public virtual DbSet<WikiUserEntity> WikiUsers { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    protected static partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
