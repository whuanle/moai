// <copyright file="WikiConfiguration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

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
        entity.Property(e => e.EmbeddingBatchSize)
            .HasComment("批处理大小")
            .HasColumnType("int(11)")
            .HasColumnName("embedding_batch_size");
        entity.Property(e => e.EmbeddingDimensions)
            .HasComment("知识库向量维度")
            .HasColumnType("int(11)")
            .HasColumnName("embedding_dimensions");
        entity.Property(e => e.EmbeddingModelId)
            .HasComment("向量化模型的id")
            .HasColumnType("int(11)")
            .HasColumnName("embedding_model_id");
        entity.Property(e => e.EmbeddingModelTokenizer)
            .HasMaxLength(20)
            .HasDefaultValueSql("''")
            .HasComment("使用的分词器")
            .HasColumnName("embedding_model_tokenizer");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsLock)
            .HasComment("是否已被锁定配置")
            .HasColumnName("is_lock");
        entity.Property(e => e.IsPublic)
            .HasComment("是否公开，公开后所有人都可以搜索到")
            .HasColumnName("is_public");
        entity.Property(e => e.MaxRetries)
            .HasDefaultValueSql("'3'")
            .HasComment("最大失败重试次数")
            .HasColumnType("int(11)")
            .HasColumnName("max_retries");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("知识库名称")
            .HasColumnName("name");
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
