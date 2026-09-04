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
