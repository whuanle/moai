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
/// Agent 应用配置，与 app 一一对应（app_type=0）.
/// </summary>
internal partial class AppAgentConfigConfiguration : IEntityTypeConfiguration<AppAgentConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAgentConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_agent_config_pkey");

        entity.ToTable("app_agent_config", tb => tb.HasComment("Agent 应用配置，与 app 一一对应（app_type=0）"));

        entity.HasIndex(e => e.AppId, "idx_app_agent_config_app_id_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_app_agent_config_team_id");

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
        entity.Property(e => e.ExecutionSettings)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("对话影响参数，JSON 对象文本（temperature/topP/maxTokens 等），空为 '{}'")
            .HasColumnName("execution_settings");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除（legacy bigint 约定）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("对话使用的模型ID，逻辑关联ai_model.id（uuid）")
            .HasColumnName("model_id");
        entity.Property(e => e.OpeningStatement)
            .HasMaxLength(4000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("对话开场白，最长4000字符，空为''")
            .HasColumnName("opening_statement");
        entity.Property(e => e.OpeningStatementEnabled)
            .HasComment("是否启用对话开场白")
            .HasColumnName("opening_statement_enabled");
        entity.Property(e => e.Plugins)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("绑定的插件ID列表，JSON 数组文本，元素为 plugin.id（uuid 字符串），如 '[\"...\"]'")
            .HasColumnName("plugins");
        entity.Property(e => e.Prompt)
            .HasMaxLength(4000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("系统提示词，最长4000字符")
            .HasColumnName("prompt");
        entity.Property(e => e.Prompts)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("开放给用户选择的可选专家提示词ID列表，JSON int 数组文本，元素为 prompt.id，如 [1,2]")
            .HasColumnName("prompts");
        entity.Property(e => e.PublishedConfig)
            .HasComment("发布配置快照 JSON（camelCase：prompt/modelId/wikiIds/plugins/skills/executionSettings/openingStatement/openingStatementEnabled/quickInputs），发布应用时写入，正式会话按此快照执行；null=从未发布")
            .HasColumnName("published_config");
        entity.Property(e => e.QuickInputs)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("快捷输入列表，JSON 数组文本，元素为字符串（管理员配置，用户在对话欢迎态点击即发送），空为'[]'")
            .HasColumnName("quick_inputs");
        entity.Property(e => e.Skills)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("绑定的技能ID列表，JSON 数组文本，元素为 skill.id（uuid 字符串），如 [\"...\"]")
            .HasColumnName("skills");
        entity.Property(e => e.Status)
            .HasComment("配置状态，0=草稿有未发布变更 1=当前草稿与已发布一致")
            .HasColumnName("status");
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
        entity.Property(e => e.WikiIds)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("绑定的知识库ID列表，JSON 数组文本，元素为 wiki.id（整数），如 '[1,2]'")
            .HasColumnName("wiki_ids");
        entity.Property(e => e.WorkflowApps)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("绑定的流程应用ID列表（作为工具使用），JSON 数组文本，元素为 app.id（uuid 字符串，须为本团队已发布流程应用），空为'[]'")
            .HasColumnName("workflow_apps");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAgentConfigEntity> modelBuilder);
}
