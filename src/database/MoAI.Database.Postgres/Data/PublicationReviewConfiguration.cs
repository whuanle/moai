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
/// 上架审核，应用/提示词等资源公开到平台前需系统管理员审批.
/// </summary>
internal partial class PublicationReviewConfiguration : IEntityTypeConfiguration<PublicationReviewEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PublicationReviewEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("publication_review_pkey");

        entity.ToTable("publication_review", tb => tb.HasComment("上架审核，资源公开到平台前需系统管理员审批"));

        // 同一资源同时只能有一条待审核申请
        entity
            .HasIndex(e => new { e.ResourceType, e.ResourceId }, "ux_publication_review_resource_pending")
            .IsUnique()
            .HasFilter("is_deleted = 0 AND state = 0");

        entity.HasIndex(e => e.TeamId, "idx_publication_review_team_id");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.ResourceType)
            .HasComment("资源类型，见 PublicationResourceType（应用=0，提示词=1）")
            .HasColumnName("resource_type");
        entity.Property(e => e.ResourceId)
            .HasMaxLength(64)
            .HasComment("资源 id 字符串，应用为 app.id（uuid），提示词为 prompt.id（数字）")
            .HasColumnName("resource_id");
        entity.Property(e => e.ResourceName)
            .HasMaxLength(100)
            .HasComment("资源名称快照，申请时的资源名称")
            .HasColumnName("resource_name");
        entity.Property(e => e.TeamId)
            .HasComment("资源所属团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.ApplyReason)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("申请说明")
            .HasColumnName("apply_reason");
        entity.Property(e => e.State)
            .HasComment("审核状态，见 PublicationState（待审核=0，已通过=1，已驳回=2）")
            .HasColumnName("state");
        entity.Property(e => e.ReviewComment)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("审批意见")
            .HasColumnName("review_comment");
        entity.Property(e => e.ReviewTime)
            .HasComment("审批时间，未审批为 null")
            .HasColumnName("review_time");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人（申请人）")
            .HasColumnName("create_user_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人（审批人）")
            .HasColumnName("update_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PublicationReviewEntity> modelBuilder);
}
