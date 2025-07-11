// <copyright file="FileConfiguration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 文件列表.
/// </summary>
public partial class FileConfiguration : IEntityTypeConfiguration<FileEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("file", tb => tb.HasComment("文件列表"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.ContentType)
            .HasMaxLength(50)
            .HasComment("文件类型")
            .HasColumnName("content_type");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FileMd5)
            .HasMaxLength(50)
            .HasComment("md5")
            .HasColumnName("file_md5");
        entity.Property(e => e.FileName)
            .HasMaxLength(50)
            .HasComment("文件名称,对于共用文件无意义,私有文件自行存储文件名称")
            .HasColumnName("file_name");
        entity.Property(e => e.FileSize)
            .HasComment("文件大小")
            .HasColumnType("int(11)")
            .HasColumnName("file_size");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublic)
            .HasComment("是否公开")
            .HasColumnName("is_public");
        entity.Property(e => e.IsUploaded)
            .HasComment("是否已经上传")
            .HasColumnName("is_uploaded");
        entity.Property(e => e.ObjectKey)
            .HasMaxLength(1024)
            .HasComment("文件路径")
            .HasColumnName("object_key");
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

    partial void OnConfigurePartial(EntityTypeBuilder<FileEntity> modelBuilder);
}
