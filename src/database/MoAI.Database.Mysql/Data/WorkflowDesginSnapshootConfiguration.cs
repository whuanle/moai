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
/// 流程设计快照，每次发布都会留下快照.
/// </summary>
public partial class WorkflowDesginSnapshootConfiguration : IEntityTypeConfiguration<WorkflowDesginSnapshootEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkflowDesginSnapshootEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("workflow_desgin_snapshoot", tb => tb.HasComment("流程设计快照，每次发布都会留下快照"));

        entity.Property(e => e.Id)
            .HasMaxLength(16)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.FunctionDesign)
            .HasDefaultValueSql("'{}'")
            .HasComment("功能设计")
            .HasColumnName("function_design");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.UiDesign)
            .HasDefaultValueSql("'{}'")
            .HasComment("ui设计")
            .HasColumnName("ui_design");
        entity.Property(e => e.Version)
            .HasDefaultValueSql("'1'")
            .HasComment("版本")
            .HasColumnType("mediumtext")
            .HasColumnName("version");
        entity.Property(e => e.WorkflowDesginId)
            .HasMaxLength(16)
            .HasComment("流程id")
            .HasColumnName("workflow_desgin_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WorkflowDesginSnapshootEntity> modelBuilder);
}
