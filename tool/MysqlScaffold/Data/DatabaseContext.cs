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
    /// 授权模型给哪些团队使用.
    /// </summary>
    public virtual DbSet<AiModelAuthorizationEntity> AiModelAuthorizations { get; set; }

    /// <summary>
    /// ai模型使用量限制，只能用于系统模型.
    /// </summary>
    public virtual DbSet<AiModelLimitEntity> AiModelLimits { get; set; }

    /// <summary>
    /// 统计不同模型的token使用量，该表不是实时刷新的.
    /// </summary>
    public virtual DbSet<AiModelTokenAuditEntity> AiModelTokenAudits { get; set; }

    /// <summary>
    /// 模型使用日志,记录每次请求使用记录.
    /// </summary>
    public virtual DbSet<AiModelUseageLogEntity> AiModelUseageLogs { get; set; }

    /// <summary>
    /// 授权私有插件给哪些团队使用.
    /// </summary>
    public virtual DbSet<PluginAuthorizationEntity> PluginAuthorizations { get; set; }

    /// <summary>
    /// 应用.
    /// </summary>
    public virtual DbSet<AppEntity> Apps { get; set; }

    /// <summary>
    /// ai助手表.
    /// </summary>
    public virtual DbSet<AppAssistantChatEntity> AppAssistantChats { get; set; }

    /// <summary>
    /// 对话历史，不保存实际历史记录.
    /// </summary>
    public virtual DbSet<AppAssistantChatHistoryEntity> AppAssistantChatHistories { get; set; }

    /// <summary>
    /// 普通应用.
    /// </summary>
    public virtual DbSet<AppCommonEntity> AppCommons { get; set; }

    /// <summary>
    /// 普通应用对话表.
    /// </summary>
    public virtual DbSet<AppCommonChatEntity> AppCommonChats { get; set; }

    /// <summary>
    /// 对话历史，不保存实际历史记录.
    /// </summary>
    public virtual DbSet<AppCommonChatHistoryEntity> AppCommonChatHistories { get; set; }

    /// <summary>
    /// 分类.
    /// </summary>
    public virtual DbSet<ClassifyEntity> Classifies { get; set; }

    /// <summary>
    /// 系统接入.
    /// </summary>
    public virtual DbSet<ExternalAppEntity> ExternalApps { get; set; }

    /// <summary>
    /// 外部系统的用户.
    /// </summary>
    public virtual DbSet<ExternalUserEntity> ExternalUsers { get; set; }

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
    /// 自定义插件.
    /// </summary>
    public virtual DbSet<PluginCustomEntity> PluginCustoms { get; set; }

    /// <summary>
    /// 插件函数.
    /// </summary>
    public virtual DbSet<PluginFunctionEntity> PluginFunctions { get; set; }

    /// <summary>
    /// 插件使用量限制.
    /// </summary>
    public virtual DbSet<PluginLimitEntity> PluginLimits { get; set; }

    /// <summary>
    /// 插件使用日志.
    /// </summary>
    public virtual DbSet<PluginLogEntity> PluginLogs { get; set; }

    /// <summary>
    /// 内置插件.
    /// </summary>
    public virtual DbSet<PluginNativeEntity> PluginNatives { get; set; }

    /// <summary>
    /// 内置插件.
    /// </summary>
    public virtual DbSet<PluginToolEntity> PluginTools { get; set; }

    /// <summary>
    /// 提示词.
    /// </summary>
    public virtual DbSet<PromptEntity> Prompts { get; set; }

    /// <summary>
    /// 系统设置.
    /// </summary>
    public virtual DbSet<SettingEntity> Settings { get; set; }

    /// <summary>
    /// 团队.
    /// </summary>
    public virtual DbSet<TeamEntity> Teams { get; set; }

    /// <summary>
    /// 团队成员.
    /// </summary>
    public virtual DbSet<TeamUserEntity> TeamUsers { get; set; }

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
    /// 文档切片预览.
    /// </summary>
    public virtual DbSet<WikiDocumentChunkContentPreviewEntity> WikiDocumentChunkContentPreviews { get; set; }

    /// <summary>
    /// 切片元数据内容表（提问/提纲/摘要）.
    /// </summary>
    public virtual DbSet<WikiDocumentChunkDerivativePreviewEntity> WikiDocumentChunkDerivativePreviews { get; set; }

    /// <summary>
    /// 切片向量化内容.
    /// </summary>
    public virtual DbSet<WikiDocumentChunkEmbeddingEntity> WikiDocumentChunkEmbeddings { get; set; }

    /// <summary>
    /// 知识库插件配置.
    /// </summary>
    public virtual DbSet<WikiPluginConfigEntity> WikiPluginConfigs { get; set; }

    /// <summary>
    /// 知识库文档关联任务，这里的任务都是成功的.
    /// </summary>
    public virtual DbSet<WikiPluginConfigDocumentEntity> WikiPluginConfigDocuments { get; set; }

    /// <summary>
    /// 知识库文档关联任务.
    /// </summary>
    public virtual DbSet<WikiPluginConfigDocumentStateEntity> WikiPluginConfigDocumentStates { get; set; }

    /// <summary>
    /// 工作任务.
    /// </summary>
    public virtual DbSet<WorkerTaskEntity> WorkerTasks { get; set; }

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
