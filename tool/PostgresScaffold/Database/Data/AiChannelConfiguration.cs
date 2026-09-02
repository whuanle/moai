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
/// 模型渠道.
/// </summary>
internal partial class AiChannelConfiguration : IEntityTypeConfiguration<AiChannelEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiChannelEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("ai_channel_pkey");

        entity.ToTable("ai_channel", tb => tb.HasComment("模型渠道"));

        entity.HasIndex(e => e.ProtocolFamily, "idx_ai_channel_protocol_family");

        entity.HasIndex(e => e.ProviderKey, "idx_ai_channel_provider_key");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ApiKey)
            .HasMaxLength(500)
            .HasComment("密钥")
            .HasColumnName("api_key");
        entity.Property(e => e.BaseUrl)
            .HasMaxLength(1000)
            .HasComment("接入端点")
            .HasColumnName("base_url");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人 id")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(1000)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.Enabled)
            .HasDefaultValue(true)
            .HasComment("是否启用")
            .HasColumnName("enabled");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("渠道名称")
            .HasColumnName("name");
        entity.Property(e => e.ProtocolFamily)
            .HasComment("协议族（openai/anthropic/google/ollama/custom）")
            .HasColumnName("protocol_family");
        entity.Property(e => e.ProviderKey)
            .HasMaxLength(50)
            .HasComment("渠道标识，对应 models.json 中的 provider id")
            .HasColumnName("provider_key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人 id")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiChannelEntity> modelBuilder);
}
