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
public partial class WorkflowDesignConfiguration : IEntityTypeConfiguration<WorkflowDesignEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkflowDesignEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("workflow_design", tb => tb.HasComment("流程设计实例表"));

        entity.Property(e => e.Id)
            .HasMaxLength(16)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(1000)
            .HasComment("头像key")
            .HasColumnName("avatar");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.FunctionDesgin)
            .HasDefaultValueSql("'{}'")
            .HasComment("功能设计，存储的是发布版本")
            .HasColumnName("function_desgin");
        entity.Property(e => e.FunctionDesignDraft)
            .HasDefaultValueSql("'{}'")
            .HasComment("功能设计草稿")
            .HasColumnName("function_design_draft");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("流程名称")
            .HasColumnName("name");
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

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WorkflowDesignEntity> modelBuilder);
}
