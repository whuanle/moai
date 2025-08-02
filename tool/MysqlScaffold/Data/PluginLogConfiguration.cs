using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 插件使用日志.
/// </summary>
public partial class PluginLogConfiguration : IEntityTypeConfiguration<PluginLogEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginLogEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_log", tb => tb.HasComment("插件使用日志"));

        entity.HasIndex(e => e.Channel, "plugin_log_channel_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.Channel)
            .HasMaxLength(10)
            .HasDefaultValueSql("'0'")
            .HasComment("渠道")
            .HasColumnName("channel");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.PluginId)
            .HasComment("插件id")
            .HasColumnType("int(11)")
            .HasColumnName("plugin_id");
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
        entity.Property(e => e.UseriId)
            .HasComment("用户id")
            .HasColumnType("int(11)")
            .HasColumnName("useri_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginLogEntity> modelBuilder);
}
