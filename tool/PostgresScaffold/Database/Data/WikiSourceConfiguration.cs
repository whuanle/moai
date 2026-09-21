using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database;

/// <summary>
/// 知识库外部源，飞书文档/爬虫等外部数据入口，持有同步方式与工作流预设.
/// </summary>
internal partial class WikiSourceConfiguration : IEntityTypeConfiguration<WikiSourceEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiSourceEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_source_pkey");

        entity.ToTable("wiki_source", tb => tb.HasComment("知识库外部源，飞书文档/爬虫等外部数据入口，持有同步方式与工作流预设"));

        entity.HasIndex(e => e.TeamId, "idx_wiki_source_team_id_index");

        entity.HasIndex(e => e.WikiId, "idx_wiki_source_wiki_id_index");

        entity.HasIndex(e => new { e.WikiId, e.Name }, "idx_wiki_source_wiki_name_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Config)
            .HasDefaultValueSql("''::text")
            .HasComment("外部源连接配置 JSON（飞书文档源：飞书应用连接id、节点token、空间id、是否含子文档等）")
            .HasColumnName("config");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Cron)
            .HasMaxLength(64)
            .HasDefaultValueSql("''::character varying")
            .HasComment("定时同步 cron 表达式（UTC），空串表示未开启定时同步")
            .HasColumnName("cron");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsEnable)
            .HasDefaultValue(true)
            .HasComment("启用，停用后不再手动/定时/事件同步")
            .HasColumnName("is_enable");
        entity.Property(e => e.IsEventSubscription)
            .HasComment("是否开启飞书事件订阅，开启后文档变更事件到达即触发重新拉取")
            .HasColumnName("is_event_subscription");
        entity.Property(e => e.LastSyncMessage)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("最近一次同步结果摘要或错误信息")
            .HasColumnName("last_sync_message");
        entity.Property(e => e.LastSyncStatus)
            .HasComment("最近一次同步状态，见 WikiSourceSyncStatus（0=未同步，1=成功，2=失败）")
            .HasColumnName("last_sync_status");
        entity.Property(e => e.LastSyncTime)
            .HasComment("最近一次同步时间")
            .HasColumnName("last_sync_time");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("外部源名称，知识库内唯一")
            .HasColumnName("name");
        entity.Property(e => e.SourceType)
            .HasComment("外部源类型，见 WikiSourceType（0=飞书文档，1=爬虫）")
            .HasColumnName("source_type");
        entity.Property(e => e.TeamId)
            .HasComment("团队id，冗余便于权限校验与查询")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");
        entity.Property(e => e.WorkflowConfig)
            .HasDefaultValueSql("''::text")
            .HasComment("外部源工作流配置 JSON（切割/元数据/向量化三步预设），空串表示回退知识库默认工作流")
            .HasColumnName("workflow_config");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiSourceEntity> modelBuilder);
}
