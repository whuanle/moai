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
/// 插件使用日志.
/// </summary>
internal partial class PluginLogConfiguration : IEntityTypeConfiguration<PluginLogEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginLogEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65840_primary");

        entity.ToTable("plugin_log", tb => tb.HasComment("插件使用日志"));

        entity.HasIndex(e => e.Channel, "idx_65840_plugin_log_channel_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Channel)
            .HasMaxLength(10)
            .HasDefaultValueSql("'0'::character varying")
            .HasComment("渠道")
            .HasColumnName("channel");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.PluginId)
            .HasComment("插件id")
            .HasColumnName("plugin_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UseriId)
            .HasComment("用户id")
            .HasColumnName("useri_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginLogEntity> modelBuilder);
}
