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
/// 流程应用编排配置，与 app 一一对应（app_type=1）.
/// </summary>
internal partial class AppWorkflowConfigConfiguration : IEntityTypeConfiguration<AppWorkflowConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppWorkflowConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_workflow_config_pkey");

        entity.ToTable("app_workflow_config", tb => tb.HasComment("流程应用编排配置，与 app 一一对应（app_type=1）"));

        entity.HasIndex(e => e.AppId, "idx_app_workflow_config_app_id_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_app_workflow_config_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("配置ID")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("所属应用ID，逻辑关联app.id（1:1，不建物理外键）")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DraftDefinition)
            .HasComment("草稿流程定义 JSON（引擎 WorkflowDefinition 契约：nodes + connections + ui）")
            .HasColumnName("draft_definition");
        entity.Property(e => e.DraftEditorData)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("草稿编辑器原始 JSON（FlowGram 画布 toJSON 产物，用于无损还原画布），空为 '{}'")
            .HasColumnName("draft_editor_data");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除（legacy bigint 约定）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.PublishTime)
            .HasComment("最近发布时间，从未发布为 null")
            .HasColumnName("publish_time");
        entity.Property(e => e.PublishedDefinition)
            .HasComment("已发布定义快照 JSON，发布后不可变；从未发布为 null")
            .HasColumnName("published_definition");
        entity.Property(e => e.Status)
            .HasComment("状态，0=草稿有未发布变更（或从未发布） 1=当前草稿已发布（草稿与已发布版本一致）")
            .HasColumnName("status");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.Version)
            .HasComment("当前已发布版本号，发布一次递增 1，0=从未发布")
            .HasColumnName("version");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppWorkflowConfigEntity> modelBuilder);
}
