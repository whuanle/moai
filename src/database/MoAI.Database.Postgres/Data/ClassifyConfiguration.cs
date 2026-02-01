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
/// 分类.
/// </summary>
internal partial class ClassifyConfiguration : IEntityTypeConfiguration<ClassifyEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClassifyEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("classify_pkey");

        entity.ToTable("classify", tb => tb.HasComment("分类"));

        entity.HasIndex(e => e.Name, "classify_name_index");

        entity.HasIndex(e => e.Type, "classify_type_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("分类描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("分类名称")
            .HasColumnName("name");
        entity.Property(e => e.Type)
            .HasMaxLength(10)
            .HasComment("分类类型")
            .HasColumnName("type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ClassifyEntity> modelBuilder);
}
