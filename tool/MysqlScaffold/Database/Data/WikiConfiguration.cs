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

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("团队头像")
            .HasColumnName("avatar");
        entity.Property(e => e.Counter)
            .HasComment("计数器")
            .HasColumnType("int(11)")
            .HasColumnName("counter");
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
            .HasComment("知识库描述")
            .HasColumnName("description");
        entity.Property(e => e.EmbeddingDimensions)
            .HasDefaultValueSql("'1024'")
            .HasComment("知识库向量维度")
            .HasColumnType("int(11)")
            .HasColumnName("embedding_dimensions");
        entity.Property(e => e.EmbeddingModelId)
            .HasComment("向量化模型的id")
            .HasColumnType("int(11)")
            .HasColumnName("embedding_model_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsLock)
            .HasComment("是否已被锁定配置")
            .HasColumnName("is_lock");
        entity.Property(e => e.IsPublic)
            .HasComment("是否公开，公开后所有人都可以使用，但是不能进去操作")
            .HasColumnName("is_public");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("知识库名称")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("团队id，不填则是个人知识库")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
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

    partial void OnConfigurePartial(EntityTypeBuilder<WikiEntity> modelBuilder);
}
