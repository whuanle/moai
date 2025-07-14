﻿using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 插件.
/// </summary>
public partial class PluginConfiguration : IEntityTypeConfiguration<PluginEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin", tb => tb.HasComment("插件"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("注释")
            .HasColumnName("description");
        entity.Property(e => e.Headers)
            .HasComment("头部")
            .HasColumnType("text")
            .HasColumnName("headers");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.OpenapiFileId)
            .HasComment("文件id")
            .HasColumnType("int(11)")
            .HasColumnName("openapi_file_id");
        entity.Property(e => e.OpenapiFileName)
            .HasMaxLength(255)
            .HasComment("文件名称")
            .HasColumnName("openapi_file_name");
        entity.Property(e => e.PluginName)
            .HasMaxLength(50)
            .HasComment("插件名称")
            .HasColumnName("plugin_name");
        entity.Property(e => e.Queries)
            .HasComment("query参数")
            .HasColumnType("text")
            .HasColumnName("queries");
        entity.Property(e => e.Server)
            .HasMaxLength(255)
            .HasComment("服务器地址")
            .HasColumnName("server");
        entity.Property(e => e.Title)
            .HasMaxLength(50)
            .HasComment("插件标题")
            .HasColumnName("title");
        entity.Property(e => e.IsPublic)
            .HasColumnType(typeName: "tinyint(1)")
            .HasColumnName("is_public");
        entity.Property(e => e.Type)
            .HasComment("system|mcp|openapi")
            .HasColumnType("int(11)")
            .HasColumnName("type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginEntity> modelBuilder);
}
