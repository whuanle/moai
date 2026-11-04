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
/// 插件工具元数据.
/// </summary>
internal partial class PluginToolMetaConfiguration : IEntityTypeConfiguration<PluginToolMetaEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginToolMetaEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("plugin_tool_meta_pkey");

        entity.ToTable("plugin_tool_meta", tb => tb.HasComment("插件工具元数据：按（插件,工具）粒度维护补充说明、调用策略与 MCP 行为注解"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CallPolicy)
            .HasDefaultValueSql("''::text")
            .HasComment("调用策略 JSON（denyPatterns 正则数组 + action：block|confirm|audit），空串=未配置")
            .HasColumnName("call_policy");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DestructiveHint)
            .HasDefaultValueSql("false")
            .HasComment("破坏性提示（MCP destructiveHint 采集或人工标注，不构成安全保证）")
            .HasColumnName("destructive_hint");
        entity.Property(e => e.ExtraNotes)
            .HasDefaultValueSql("''::text")
            .HasComment("工具补充说明（叠加在原始描述之上进入模型工具说明）")
            .HasColumnName("extra_notes");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.PluginId)
            .HasComment("所属插件 id，逻辑关联 plugin.id")
            .HasColumnName("plugin_id");
        entity.Property(e => e.ReadOnlyHint)
            .HasDefaultValueSql("false")
            .HasComment("只读提示（MCP readOnlyHint 采集或人工标注，不构成安全保证）")
            .HasColumnName("read_only_hint");
        entity.Property(e => e.ToolKey)
            .HasMaxLength(200)
            .HasComment("工具 key（MCP/OpenAPI 为函数名，动态插件为工具 key，静态插件为 run）")
            .HasColumnName("tool_key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        entity.HasIndex(e => e.PluginId, "idx_plugin_tool_meta_plugin_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginToolMetaEntity> modelBuilder);
}
