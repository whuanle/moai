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
/// 团队智能体，主智能体 + 多个子智能体的编排定义资产.
/// </summary>
internal partial class TeamAgentConfiguration : IEntityTypeConfiguration<TeamAgentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamAgentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_agent_pkey");

        entity.ToTable("team_agent", tb => tb.HasComment("团队智能体，主智能体 + 多个子智能体的编排定义资产"));

        entity.HasIndex(e => e.TeamId, "idx_team_agent_team_id");

        entity.HasIndex(e => new { e.TeamId, e.Name }, "idx_team_agent_team_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        // key 列存量行为空串：唯一索引排除空串，避免存量行冲突；新行 key 必填即受唯一约束
        entity.HasIndex(e => new { e.TeamId, e.Key }, "idx_team_agent_team_key_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0) AND (key <> '')");

        entity.Property(e => e.Id)
            .HasComment("智能体ID")
            .HasColumnName("id");
        entity.Property(e => e.Key)
            .HasMaxLength(30)
            .HasComment("智能体标识 key，团队内唯一（小写字母开头的字母/数字/下划线），创建后不可变更；Phase 2 将作为工具名暴露")
            .HasColumnName("key");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间，审计钩子自动填充，默认timezone(utc,now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人用户ID，审计钩子插入时自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(500)
            .HasDefaultValueSql("''::character varying")
            .HasComment("智能体描述，空串=未填写")
            .HasColumnName("description");
        entity.Property(e => e.AvatarObjectKey)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("头像文件 objectKey，空串=未设置")
            .HasColumnName("avatar_object_key");
        entity.Property(e => e.Enabled)
            .HasComment("是否启用，停用后不可被应用绑定与运行")
            .HasColumnName("enabled");
        entity.Property(e => e.IsSystem)
            .HasComment("是否系统内置智能体：团队创建时自动种入（如规划/构建），不可删除、可编辑定制")
            .HasColumnName("is_system");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.MainGraphIds)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("主智能体知识图谱依赖，JSON 数字数组")
            .HasColumnName("main_graph_ids");
        entity.Property(e => e.MainPluginIds)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("主智能体插件依赖，JSON uuid 字符串数组")
            .HasColumnName("main_plugin_ids");
        entity.Property(e => e.MainPrompt)
            .HasDefaultValueSql("''::text")
            .HasComment("主智能体系统提示词")
            .HasColumnName("main_prompt");
        entity.Property(e => e.MainWikiIds)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("主智能体知识库依赖，JSON 数字数组")
            .HasColumnName("main_wiki_ids");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("智能体名称（展示名，可中文），团队内唯一")
            .HasColumnName("name");
        entity.Property(e => e.Subs)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("子智能体列表，JSON 数组（key/name/description/prompt/pluginIds/wikiIds/graphIds），最多 20 个")
            .HasColumnName("subs");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联team.id（仓库约定不建物理外键）")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间，审计钩子插入/更新/删除时自动刷新")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人用户ID，审计钩子更新/删除时自动填充")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamAgentEntity> modelBuilder);
}
