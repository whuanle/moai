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

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ExpirationTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("过期时间")
            .HasColumnType("datetime")
            .HasColumnName("expiration_time");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LimitValue)
            .HasComment("限制值")
            .HasColumnType("int(11)")
            .HasColumnName("limit_value");
        entity.Property(e => e.PluginId)
            .HasComment("插件id")
            .HasColumnType("int(11)")
            .HasColumnName("plugin_id");
        entity.Property(e => e.RuleType)
            .HasComment("限制的规则类型,每天/总额/有效期")
            .HasColumnType("int(11)")
            .HasColumnName("rule_type");
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
        entity.Property(e => e.UserId)
            .HasComment("用户id")
            .HasColumnType("int(11)")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginLimitEntity> modelBuilder);
}
