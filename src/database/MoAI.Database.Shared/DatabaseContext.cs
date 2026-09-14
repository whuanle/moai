using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext : DbContext
{
    /// <summary>
    /// 应用接入.
    /// </summary>
    public virtual DbSet<AccessAppEntity> AccessApps { get; set; }

    /// <summary>
    /// 模型渠道.
    /// </summary>
    public virtual DbSet<AiChannelEntity> AiChannels { get; set; }

    /// <summary>
    /// 模型.
    /// </summary>
    public virtual DbSet<AiModelEntity> AiModels { get; set; }

    /// <summary>
    /// 授权模型给哪些团队使用，额度以团队为单位共享.
    /// </summary>
    public virtual DbSet<AiModelAuthorizationEntity> AiModelAuthorizations { get; set; }

    /// <summary>
    /// ai模型额度规则，一行=某主体在一个重置周期内的tokens上限，只能用于系统模型.
    /// </summary>
    public virtual DbSet<AiModelLimitEntity> AiModelLimits { get; set; }

    /// <summary>
    /// ai模型额度余额，按规则维度记录当前周期已消耗与剩余，剩余=total_limit-used_tokens.
    /// </summary>
    public virtual DbSet<AiModelQuotumEntity> AiModelQuota { get; set; }

    /// <summary>
    /// 统计不同模型的token使用量，该表不是实时刷新的，按模型+团队+用户+业务来源维度累加.
    /// </summary>
    public virtual DbSet<AiModelTokenAuditEntity> AiModelTokenAudits { get; set; }

    /// <summary>
    /// 模型使用日志,记录每次请求使用记录.
    /// </summary>
    public virtual DbSet<AiModelUsageLogEntity> AiModelUsageLogs { get; set; }

    /// <summary>
    /// 应用.
    /// </summary>
    public virtual DbSet<AppEntity> Apps { get; set; }

    /// <summary>
    /// Agent 应用配置，与 app 一一对应（app_type=0）.
    /// </summary>
    public virtual DbSet<AppAgentConfigEntity> AppAgentConfigs { get; set; }

    /// <summary>
    /// Agent 应用会话消息（对话历史），追加写，按 seq 排序.
    /// </summary>
    public virtual DbSet<AppAgentMessageEntity> AppAgentMessages { get; set; }

    /// <summary>
    /// Agent 应用会话（会话列表），一个会话属于一个应用与一个用户.
    /// </summary>
    public virtual DbSet<AppAgentSessionEntity> AppAgentSessions { get; set; }

    /// <summary>
    /// 分类.
    /// </summary>
    public virtual DbSet<ClassifyEntity> Classifies { get; set; }

    /// <summary>
    /// 文件列表.
    /// </summary>
    public virtual DbSet<FileEntity> Files { get; set; }

    public virtual DbSet<KnowledgeGraphEntity> KnowledgeGraphs { get; set; }

    public virtual DbSet<KnowledgeGraphEntityTypeEntity> KnowledgeGraphEntityTypes { get; set; }

    public virtual DbSet<KnowledgeGraphRelationTypeEntity> KnowledgeGraphRelationTypes { get; set; }

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
    /// 内置插件.
    /// </summary>
    public virtual DbSet<PluginDynamicEntity> PluginDynamics { get; set; }

    /// <summary>
    /// 插件函数.
    /// </summary>
    public virtual DbSet<PluginFunctionEntity> PluginFunctions { get; set; }

    /// <summary>
    /// 内置插件.
    /// </summary>
    public virtual DbSet<PluginStaticEntity> PluginStatics { get; set; }

    /// <summary>
    /// 授权私有系统插件给哪些团队使用.
    /// </summary>
    public virtual DbSet<PluginTeamAuthorizationEntity> PluginTeamAuthorizations { get; set; }

    /// <summary>
    /// 系统设置.
    /// </summary>
    public virtual DbSet<SettingEntity> Settings { get; set; }

    /// <summary>
    /// 技能.
    /// </summary>
    public virtual DbSet<SkillEntity> Skills { get; set; }

    /// <summary>
    /// 团队，知识库/插件等资源的管理单元.
    /// </summary>
    public virtual DbSet<TeamEntity> Teams { get; set; }

    /// <summary>
    /// 团队模型网关API密钥，团队管理员创建并维护，团队成员使用密钥通过 /v1 开放接口调用团队已授权的模型.
    /// </summary>
    public virtual DbSet<TeamApiKeyEntity> TeamApiKeys { get; set; }

    /// <summary>
    /// 团队成员，用户与团队多对多关联.
    /// </summary>
    public virtual DbSet<TeamUserEntity> TeamUsers { get; set; }

    /// <summary>
    /// 团队变量，插件配置以 ${key} 引用.
    /// </summary>
    public virtual DbSet<TeamVariableEntity> TeamVariables { get; set; }

    /// <summary>
    /// 用户.
    /// </summary>
    public virtual DbSet<UserEntity> Users { get; set; }

    /// <summary>
    /// oauth2.0对接.
    /// </summary>
    public virtual DbSet<UserOauthConnectionEntity> UserOauthConnections { get; set; }

    /// <summary>
    /// 知识库.
    /// </summary>
    public virtual DbSet<WikiEntity> Wikis { get; set; }

    /// <summary>
    /// 知识库文档.
    /// </summary>
    public virtual DbSet<WikiDocumentEntity> WikiDocuments { get; set; }

    /// <summary>
    /// 文档切片内容.
    /// </summary>
    public virtual DbSet<WikiDocumentChunkContentEntity> WikiDocumentChunkContents { get; set; }

    /// <summary>
    /// 切片向量化内容.
    /// </summary>
    public virtual DbSet<WikiDocumentChunkEmbeddingEntity> WikiDocumentChunkEmbeddings { get; set; }

    /// <summary>
    /// 切片元数据内容表（提问/提纲/摘要）.
    /// </summary>
    public virtual DbSet<WikiDocumentChunkMetadatumEntity> WikiDocumentChunkMetadata { get; set; }

    /// <summary>
    /// 文档内容.
    /// </summary>
    public virtual DbSet<WikiDocumentContentEntity> WikiDocumentContents { get; set; }

    /// <summary>
    /// 工作任务.
    /// </summary>
    public virtual DbSet<WorkerTaskEntity> WorkerTasks { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    protected partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
