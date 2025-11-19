#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext : DbContext
{
    /// <summary>
    /// IServiceProvider.
    /// </summary>
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
    /// ai助手表.
    /// </summary>
    public virtual DbSet<AppAssistantChatEntity> AppAssistantChats { get; set; }

    /// <summary>
    /// 对话历史，不保存实际历史记录.
    /// </summary>
    public virtual DbSet<AppAssistantChatHistoryEntity> AppAssistantChatHistories { get; set; }

    /// <summary>
    /// 分类.
    /// </summary>
    public virtual DbSet<ClassifyEntity> Classifies { get; set; }

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
    /// 内置插件.
    /// </summary>
    public virtual DbSet<PluginNativeEntity> PluginInternals { get; set; }

    /// <summary>
    /// 插件使用量限制.
    /// </summary>
    public virtual DbSet<PluginLimitEntity> PluginLimits { get; set; }

    /// <summary>
    /// 插件使用日志.
    /// </summary>
    public virtual DbSet<PluginLogEntity> PluginLogs { get; set; }

    /// <summary>
    /// 提示词.
    /// </summary>
    public virtual DbSet<PromptEntity> Prompts { get; set; }

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
    /// 文档向量化任务状态.
    /// </summary>
    public virtual DbSet<WikiDocumentTaskEntity> WikiDocumentTasks { get; set; }

    /// <summary>
    /// 知识库成员.
    /// </summary>
    public virtual DbSet<WikiUserEntity> WikiUsers { get; set; }

    /// <summary>
    /// wiki网页抓取.
    /// </summary>
    public virtual DbSet<WikiWebConfigEntity> WikiWebConfigs { get; set; }

    /// <summary>
    /// 爬虫页面进度列表.
    /// </summary>
    public virtual DbSet<WikiWebCrawlePageStateEntity> WikiWebCrawlePageStates { get; set; }

    /// <summary>
    /// web爬虫状态.
    /// </summary>
    public virtual DbSet<WikiWebCrawleTaskEntity> WikiWebCrawleTasks { get; set; }

    /// <summary>
    /// 抓取的页面文档.
    /// </summary>
    public virtual DbSet<WikiWebDocumentEntity> WikiWebDocuments { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    protected static partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
