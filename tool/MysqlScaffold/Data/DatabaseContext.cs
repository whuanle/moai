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
    /// ai助手表.
    /// </summary>
    public virtual DbSet<AppAssistantChatEntity> AppAssistantChats { get; set; }

    /// <summary>
    /// 对话历史，不保存实际历史记录.
    /// </summary>
    public virtual DbSet<AppAssistantChatHistoryEntity> AppAssistantChatHistories { get; set; }

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
    /// 提示词.
    /// </summary>
    public virtual DbSet<PromptEntity> Prompts { get; set; }

    /// <summary>
    /// 提示词分类.
    /// </summary>
    public virtual DbSet<PromptClassEntity> PromptClasses { get; set; }

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);
        OnModelCreatingPartial(modelBuilder);
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
