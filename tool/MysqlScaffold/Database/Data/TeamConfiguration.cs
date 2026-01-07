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
/// 团队.
/// </summary>
public partial class TeamConfiguration : IEntityTypeConfiguration<TeamEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("team", tb => tb.HasComment("团队"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.Avatar)
            .HasDefaultValueSql("''")
            .HasComment("团队头像,objectkey");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description)
            .HasDefaultValueSql("''")
            .HasComment("团队描述");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Name).HasComment("团队名称");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamEntity> modelBuilder);
}
