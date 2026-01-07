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
public partial class ClassifyConfiguration : IEntityTypeConfiguration<ClassifyEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClassifyEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("classify", tb => tb.HasComment("分类"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description)
            .HasDefaultValueSql("''")
            .HasComment("分类描述");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Name).HasComment("分类名称");
        entity.Property(e => e.Type).HasComment("分类类型");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ClassifyEntity> modelBuilder);
}
