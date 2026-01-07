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
/// 插件使用量限制.
/// </summary>
public partial class PluginLimitConfiguration : IEntityTypeConfiguration<PluginLimitEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginLimitEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_limit", tb => tb.HasComment("插件使用量限制"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.ExpirationTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("过期时间");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.LimitValue).HasComment("限制值");
        entity.Property(e => e.PluginId).HasComment("插件id");
        entity.Property(e => e.RuleType).HasComment("限制的规则类型,每天/总额/有效期");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.UserId).HasComment("用户id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginLimitEntity> modelBuilder);
}
