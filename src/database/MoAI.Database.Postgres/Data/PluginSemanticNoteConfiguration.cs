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
/// 插件语义条目.
/// </summary>
internal partial class PluginSemanticNoteConfiguration : IEntityTypeConfiguration<PluginSemanticNoteEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginSemanticNoteEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("plugin_semantic_note_pkey");

        entity.ToTable("plugin_semantic_note", tb => tb.HasComment("插件语义条目：按（插件,工具）粒度维护语义说明，按披露方式注入 AI 上下文"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Content)
            .HasDefaultValueSql("''::text")
            .HasComment("条目内容（markdown，如库表用途、字段说明、业务术语、示例问答）")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Mode)
            .HasDefaultValueSql("0")
            .HasComment("披露方式：0=always 常驻注入上下文；1=on_demand 经 plugin_note 元工具按需取回")
            .HasColumnName("mode");
        entity.Property(e => e.PluginId)
            .HasComment("所属插件 id，逻辑关联 plugin.id")
            .HasColumnName("plugin_id");
        entity.Property(e => e.SortOrder)
            .HasDefaultValueSql("0")
            .HasComment("排序")
            .HasColumnName("sort_order");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasComment("条目标题（按需披露时作为目录展示给模型）")
            .HasColumnName("title");
        entity.Property(e => e.ToolKey)
            .HasMaxLength(200)
            .HasDefaultValueSql("''::text")
            .HasComment("工具 key，空串=插件级条目；否则为工具 key（MCP/OpenAPI 为函数名，动态插件为工具 key）")
            .HasColumnName("tool_key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        entity.HasIndex(e => e.PluginId, "idx_plugin_semantic_note_plugin_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginSemanticNoteEntity> modelBuilder);
}
