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
/// 应用.
/// </summary>
public partial class AppConfiguration : IEntityTypeConfiguration<AppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app", tb => tb.HasComment("应用"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id");
        entity.Property(e => e.AppType).HasComment("应用类型，普通应用=0,流程编排=1");
        entity.Property(e => e.Avatar)
            .HasDefaultValueSql("''")
            .HasComment("头像 objectKey");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description).HasComment("描述");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsDisable).HasComment("禁用");
        entity.Property(e => e.IsForeign).HasComment("是否外部应用");
        entity.Property(e => e.IsPublic).HasComment("公开到团队外使用");
        entity.Property(e => e.Name).HasComment("应用名称");
        entity.Property(e => e.TeamId).HasComment("团队id");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppEntity> modelBuilder);
}
