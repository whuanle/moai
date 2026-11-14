using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database;

internal partial class KgImportJobConfiguration : IEntityTypeConfiguration<KgImportJobEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KgImportJobEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("kg_import_job_pkey");

        entity.ToTable("kg_import_job");

        entity.HasIndex(e => new { e.KnowledgeGraphId, e.Id }, "idx_kg_import_job_kg_id");

        entity.Property(e => e.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        entity.Property(e => e.AiModelId).HasColumnName("ai_model_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DoneRows).HasComment("已完成行数（落库进度）").HasColumnName("done_rows");
        entity.Property(e => e.ErrorJson).HasComment("失败原因（JSON：message/stage），成功为 null").HasColumnName("error_json");
        entity.Property(e => e.FileName).HasMaxLength(255).HasComment("导入文件名").HasColumnName("file_name");
        entity.Property(e => e.IsDeleted).HasComment("软删除，0=未删除").HasColumnName("is_deleted");
        entity.Property(e => e.KnowledgeGraphId).HasComment("所属图谱 id（仅托管图）").HasColumnName("knowledge_graph_id");
        entity.Property(e => e.ObjectKey).HasMaxLength(500).HasComment("导入文件的对象存储 key").HasColumnName("object_key");
        entity.Property(e => e.ResultJson).HasComment("结果统计 JSON，未完成为 null").HasColumnName("result_json");
        entity.Property(e => e.Status).HasMaxLength(20).HasComment("任务状态：pending/extracting/parsing/merging/done/failed/cancelled").HasColumnName("status");
        entity.Property(e => e.TeamId).HasComment("所属团队 id").HasColumnName("team_id");
        entity.Property(e => e.TotalRows).HasComment("总行数（解析出的节点+边草稿数）").HasColumnName("total_rows");
        entity.Property(e => e.Type).HasMaxLength(20).HasComment("任务类型：file/json").HasColumnName("type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
    }
}
