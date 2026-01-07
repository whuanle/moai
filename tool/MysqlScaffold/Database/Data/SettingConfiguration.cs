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
/// 系统设置.
/// </summary>
public partial class SettingConfiguration : IEntityTypeConfiguration<SettingEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettingEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("setting", tb => tb.HasComment("系统设置"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description)
            .HasDefaultValueSql("''")
            .HasComment("描述");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Key).HasComment("配置名称");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");
        entity.Property(e => e.Value).HasComment("配置值,json");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SettingEntity> modelBuilder);
}
