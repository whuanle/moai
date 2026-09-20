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
/// 用户级应用配置，(app_id, user_id) 唯一，跨会话复用.
/// </summary>
internal partial class AppUserConfigConfiguration : IEntityTypeConfiguration<AppUserConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppUserConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_user_config_pkey");

        entity.ToTable("app_user_config", tb => tb.HasComment("用户级应用配置，(app_id, user_id) 唯一，跨会话复用"));

        entity.HasIndex(e => new { e.AppId, e.UserId }, "idx_app_user_config_app_user_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_app_user_config_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("应用 id，逻辑关联 app.id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.PromptId)
            .HasComment("用户为新会话选择的专家提示词 id，0=未设置")
            .HasColumnName("prompt_id");
        entity.Property(e => e.Skills)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("用户勾选启用的技能 id 列表 JSON 数组，须为应用默认技能（app_agent_config.skills）的子集")
            .HasColumnName("skills");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队 id，逻辑关联 app.team_id，冗余用于团队维度过滤")
            .HasColumnName("team_id");
        entity.Property(e => e.ToolApprovalMode)
            .HasMaxLength(16)
            .HasDefaultValueSql("'auto'::character varying")
            .HasComment("工具审批模式：auto=自动执行；approval=重要工具调用前需人工批准")
            .HasColumnName("tool_approval_mode");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("用户 id，逻辑关联 user.id")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppUserConfigEntity> modelBuilder);
}
