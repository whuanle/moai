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
public partial class FileConfiguration : IEntityTypeConfiguration<FileEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("file", tb => tb.HasComment("文件列表"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.ContentType).HasComment("文件类型");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.FileExtension)
            .HasDefaultValueSql("''")
            .HasComment("文件扩展名");
        entity.Property(e => e.FileMd5).HasComment("md5");
        entity.Property(e => e.FileSize).HasComment("文件大小");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsUploaded).HasComment("是否已经上传完毕");
        entity.Property(e => e.ObjectKey).HasComment("文件路径");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<FileEntity> modelBuilder);
}
