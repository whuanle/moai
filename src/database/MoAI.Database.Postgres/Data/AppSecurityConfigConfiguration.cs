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
/// 应用安全配置（内容脱敏），与 app 一一对应（Agent/流程应用通用）.
/// </summary>
internal partial class AppSecurityConfigConfiguration : IEntityTypeConfiguration<AppSecurityConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppSecurityConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_security_config_pkey");

        entity.ToTable("app_security_config", tb => tb.HasComment("应用安全配置（内容脱敏），与 app 一一对应（Agent/流程应用通用）"));

        entity.HasIndex(e => e.AppId, "idx_app_security_config_app_id_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_app_security_config_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("配置ID")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("所属应用ID，逻辑关联app.id（1:1，不建物理外键）")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Enabled)
            .HasComment("是否启用内容脱敏；启用且存在至少一条规则时生效")
            .HasColumnName("enabled");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除（legacy bigint 约定）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.MaskModelOutput)
            .HasComment("是否对模型回复文本（对话正文）脱敏")
            .HasColumnName("mask_model_output");
        entity.Property(e => e.MaskToolArgs)
            .HasComment("是否对工具调用参数（调用记录展示、流程节点输入）脱敏")
            .HasColumnName("mask_tool_args");
        entity.Property(e => e.MaskToolResult)
            .HasComment("是否对工具调用结果（含错误信息、流程节点输出）脱敏")
            .HasColumnName("mask_tool_result");
        entity.Property(e => e.Rules)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("内容脱敏规则列表（工具调用结果/工具调用参数范围共用），JSON 数组文本，元素为 {name,type,pattern,replacement}，type 见 AppSecurityRuleTypes，空为 '[]'")
            .HasColumnName("rules");
        entity.Property(e => e.ModelOutputRules)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("模型回复专属脱敏规则列表，与 rules 相互独立维护，元素同 rules，空为 '[]'")
            .HasColumnName("model_output_rules");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppSecurityConfigEntity> modelBuilder);
}
