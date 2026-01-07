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
/// 知识库.
/// </summary>
public partial class WikiConfiguration : IEntityTypeConfiguration<WikiEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki", tb => tb.HasComment("知识库"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.Avatar)
            .HasDefaultValueSql("''")
            .HasComment("团队头像");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description).HasComment("知识库描述");
        entity.Property(e => e.EmbeddingDimensions)
            .HasDefaultValueSql("'1024'")
            .HasComment("知识库向量维度");
        entity.Property(e => e.EmbeddingModelId).HasComment("向量化模型的id");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsLock).HasComment("是否已被锁定配置");
        entity.Property(e => e.IsPublic).HasComment("是否公开，公开后所有人都可以使用，但是不能进去操作");
        entity.Property(e => e.Name).HasComment("知识库名称");
        entity.Property(e => e.TeamId).HasComment("团队id，不填则是个人知识库");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiEntity> modelBuilder);
}
