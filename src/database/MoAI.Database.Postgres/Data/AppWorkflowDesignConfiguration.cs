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
/// 流程设计实例表.
/// </summary>
internal partial class AppWorkflowDesignConfiguration : IEntityTypeConfiguration<AppWorkflowDesignEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppWorkflowDesignEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65701_primary");

        entity.ToTable("app_workflow_design", tb => tb.HasComment("流程设计实例表"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("应用id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FunctionDesgin)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("功能设计，存储的是发布版本")
            .HasColumnName("function_desgin");
        entity.Property(e => e.FunctionDesignDraft)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("功能设计草稿")
            .HasColumnName("function_design_draft");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublish)
            .HasDefaultValue(false)
            .HasComment("是否发布")
            .HasColumnName("is_publish");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UiDesign)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("ui设计，存储的是发布版本")
            .HasColumnName("ui_design");
        entity.Property(e => e.UiDesignDraft)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("ui设计草稿")
            .HasColumnName("ui_design_draft");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppWorkflowDesignEntity> modelBuilder);
}
