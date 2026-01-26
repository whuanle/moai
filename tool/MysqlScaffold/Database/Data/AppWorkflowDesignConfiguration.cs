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
public partial class AppWorkflowDesignConfiguration : IEntityTypeConfiguration<AppWorkflowDesignEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppWorkflowDesignEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_workflow_design", tb => tb.HasComment("流程设计实例表"));

        entity.Property(e => e.Id)
            .HasMaxLength(16)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasMaxLength(16)
            .HasComment("应用id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FunctionDesgin)
            .HasDefaultValueSql("'{}'")
            .HasComment("功能设计，存储的是发布版本")
            .HasColumnName("function_desgin");
        entity.Property(e => e.FunctionDesignDraft)
            .HasDefaultValueSql("'{}'")
            .HasComment("功能设计草稿")
            .HasColumnName("function_design_draft");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublish)
            .HasComment("是否发布")
            .HasColumnName("is_publish");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.UiDesign)
            .HasDefaultValueSql("'{}'")
            .HasComment("ui设计，存储的是发布版本")
            .HasColumnName("ui_design");
        entity.Property(e => e.UiDesignDraft)
            .HasDefaultValueSql("'{}'")
            .HasComment("ui设计草稿")
            .HasColumnName("ui_design_draft");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppWorkflowDesignEntity> modelBuilder);
}
