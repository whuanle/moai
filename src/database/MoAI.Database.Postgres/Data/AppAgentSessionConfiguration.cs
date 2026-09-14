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
/// Agent 应用会话（会话列表），一个会话属于一个应用与一个用户.
/// </summary>
internal partial class AppAgentSessionConfiguration : IEntityTypeConfiguration<AppAgentSessionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAgentSessionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_agent_session_pkey");

        entity.ToTable("app_agent_session", tb => tb.HasComment("Agent 应用会话（会话列表），一个会话属于一个应用与一个用户"));

        entity.HasIndex(e => e.AppId, "idx_app_agent_session_app_id");

        entity.HasIndex(e => new { e.AppId, e.CreateUserId, e.LastMessageTime }, "idx_app_agent_session_app_user_last_index").IsDescending(false, false, true);

        entity.HasIndex(e => e.TeamId, "idx_app_agent_session_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("会话ID")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("所属应用ID，逻辑关联app.id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("会话归属用户（内部用户为用户ID）")
            .HasColumnName("create_user_id");
        entity.Property(e => e.InputTokens)
            .HasComment("输入token累计")
            .HasColumnName("input_tokens");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除（legacy bigint 约定）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LastMessageTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("最后一条消息时间，会话列表按此倒序")
            .HasColumnName("last_message_time");
        entity.Property(e => e.OutTokens)
            .HasComment("输出token累计")
            .HasColumnName("out_tokens");
        entity.Property(e => e.State)
            .HasComment("Agent 会话冷快照（AgentSession 序列化，含上下文压缩索引），Redis 热态失效后恢复")
            .HasColumnType("jsonb")
            .HasColumnName("state");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联app.team_id")
            .HasColumnName("team_id");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasDefaultValueSql("'未命名标题'::character varying")
            .HasComment("会话标题，最长100字符，首轮对话后可由模型生成")
            .HasColumnName("title");
        entity.Property(e => e.TotalTokens)
            .HasComment("token 总数累计")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserType)
            .HasComment("发起用户类型，对齐 MoAI.Infra.Models.UserType：0=识别不到，1=外部用户，2=外部应用，3=内部普通用户（当前仅内部用户）")
            .HasColumnName("user_type");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAgentSessionEntity> modelBuilder);
}
