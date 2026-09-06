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
    /// 系统设置.
    /// </summary>
    public virtual DbSet<SettingEntity> Settings { get; set; }

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
    /// 知识库，挂在团队下的资源.
    /// </summary>
    public virtual DbSet<WikiEntity> Wikis { get; set; }

    /// <summary>
    /// 知识库文档，挂在知识库下的内容页.
    /// </summary>
    public virtual DbSet<WikiDocumentEntity> WikiDocuments { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    protected static partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
