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
/// 文件列表.
/// </summary>
internal partial class FileConfiguration : IEntityTypeConfiguration<FileEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65761_primary");

        entity.ToTable("file", tb => tb.HasComment("文件列表"));

        entity.HasIndex(e => e.FileMd5, "idx_65761_file_file_md5_index");

        entity.HasIndex(e => e.ObjectKey, "idx_65761_file_object_key_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ContentType)
            .HasMaxLength(50)
            .HasComment("文件类型")
            .HasColumnName("content_type");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FileExtension)
            .HasMaxLength(10)
            .HasDefaultValueSql("''::character varying")
            .HasComment("文件扩展名")
            .HasColumnName("file_extension");
        entity.Property(e => e.FileMd5)
            .HasMaxLength(50)
            .HasComment("md5")
            .HasColumnName("file_md5");
        entity.Property(e => e.FileSize)
            .HasComment("文件大小")
            .HasColumnName("file_size");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsUploaded)
            .HasComment("是否已经上传完毕")
            .HasColumnName("is_uploaded");
        entity.Property(e => e.ObjectKey)
            .HasMaxLength(1024)
            .HasComment("文件路径")
            .HasColumnName("object_key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<FileEntity> modelBuilder);
}
