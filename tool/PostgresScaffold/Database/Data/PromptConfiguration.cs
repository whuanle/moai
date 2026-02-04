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
internal partial class PromptConfiguration : IEntityTypeConfiguration<PromptEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PromptEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65870_primary");

        entity.ToTable("prompt", tb => tb.HasComment("提示词"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Content)
            .HasComment("提示词内容")
            .HasColumnName("content");
        entity.Property(e => e.Counter)
            .HasDefaultValue(0)
            .HasComment("计数器")
            .HasColumnName("counter");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublic)
            .HasDefaultValue(false)
            .HasComment("是否公开")
            .HasColumnName("is_public");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("名称")
            .HasColumnName("name");
        entity.Property(e => e.PromptClassId)
            .HasComment("分类id")
            .HasColumnName("prompt_class_id");
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

    partial void OnConfigurePartial(EntityTypeBuilder<PromptEntity> modelBuilder);
}
