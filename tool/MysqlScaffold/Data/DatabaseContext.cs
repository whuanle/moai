using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseOptions _contextOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="contextOptions"></param>
    public DatabaseContext(DatabaseOptions<DatabaseContext> options, IServiceProvider serviceProvider, DatabaseOptions contextOptions)
        : base(options)
    {
        _serviceProvider = serviceProvider;
        _contextOptions = contextOptions;
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
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
