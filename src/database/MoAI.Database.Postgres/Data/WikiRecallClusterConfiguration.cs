using System;
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
/// 知识库检索经验聚类.
/// </summary>
internal partial class WikiRecallClusterConfiguration : IEntityTypeConfiguration<WikiRecallClusterEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiRecallClusterEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_wiki_recall_cluster_primary");

        entity.ToTable("wiki_recall_cluster", tb => tb.HasComment("知识库检索经验聚类（query 归一化哈希唯一，沉淀验证过的命中供复用）"));

        entity.HasIndex(e => e.WikiId, "idx_wiki_recall_cluster_wiki_id");

        // 部分唯一索引（软删行不占位，删除后可重建同 hash 聚类）；存量库由启动幂等 DDL 补齐同款
        entity.HasIndex(e => new { e.WikiId, e.QueryHash }, "idx_wiki_recall_cluster_wiki_query_uindex")
            .IsUnique()
            .HasFilter("is_deleted = 0");

        entity.Property(e => e.Id)
            .HasComment("雪花主键")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.HitCount)
            .HasComment("沉淀的命中条数")
            .HasColumnName("hit_count");
        entity.Property(e => e.IndexEpoch)
            .HasComment("沉淀时的知识库索引纪元；复用时与当前纪元比对，不匹配即失效自愈")
            .HasColumnName("index_epoch");
        entity.Property(e => e.HitsJson)
            .HasComment("命中列表 JSON：[{documentId, chunkId, score, rerankScore, metadataType}]")
            .HasColumnName("hits_json");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LastRecallTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("最近一次服务（沉淀或复用）时间")
            .HasColumnName("last_recall_time");
        entity.Property(e => e.Query)
            .HasComment("归一化后的查询原文")
            .HasColumnName("query");
        entity.Property(e => e.QueryHash)
            .HasComment("归一化查询的 SHA-256")
            .HasColumnName("query_hash");
        entity.Property(e => e.RecallCount)
            .HasComment("累计服务次数（含首次沉淀；复用命中时递增）")
            .HasColumnName("recall_count");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiRecallClusterEntity> modelBuilder);
}
