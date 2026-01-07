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
/// 系统接入.
/// </summary>
public partial class ExternalAppConfiguration : IEntityTypeConfiguration<ExternalAppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalAppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("external_app", tb => tb.HasComment("系统接入"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("app_id");
        entity.Property(e => e.Avatar)
            .HasDefaultValueSql("''")
            .HasComment("头像objectKey");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description).HasComment("描述");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsDsiable).HasComment("禁用");
        entity.Property(e => e.Key).HasComment("应用密钥");
        entity.Property(e => e.Name).HasComment("应用名称");
        entity.Property(e => e.TeamId).HasComment("团队id");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ExternalAppEntity> modelBuilder);
}
