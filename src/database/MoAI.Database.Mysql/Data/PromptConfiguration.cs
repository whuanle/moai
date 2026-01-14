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
/// 提示词.
/// </summary>
public partial class PromptConfiguration : IEntityTypeConfiguration<PromptEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PromptEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("prompt", tb => tb.HasComment("提示词"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.Content)
            .HasComment("提示词内容")
            .HasColumnType("text")
            .HasColumnName("content");
        entity.Property(e => e.Counter)
            .HasComment("计数器")
            .HasColumnType("int(11)")
            .HasColumnName("counter");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublic)
            .HasComment("是否公开")
            .HasColumnName("is_public");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("名称")
            .HasColumnName("name");
        entity.Property(e => e.PromptClassId)
            .HasComment("分类id")
            .HasColumnType("int(11)")
            .HasColumnName("prompt_class_id");
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

    partial void OnConfigurePartial(EntityTypeBuilder<PromptEntity> modelBuilder);
}
