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
/// ai模型.
/// </summary>
internal partial class AiModelConfiguration : IEntityTypeConfiguration<AiModelEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ai_model", tb => tb.HasComment("ai模型"));

        entity.HasIndex(e => e.AiModelType, "ai_model_ai_model_type_index");

        entity.HasIndex(e => e.AiProvider, "ai_model_ai_provider_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.AiModelType)
            .HasMaxLength(20)
            .HasComment("模型功能类型")
            .HasColumnName("ai_model_type");
        entity.Property(e => e.AiProvider)
            .HasMaxLength(50)
            .HasComment("模型供应商")
            .HasColumnName("ai_provider");
        entity.Property(e => e.ContextWindowTokens)
            .HasDefaultValueSql("'8192'")
            .HasComment("上下文最大token数量")
            .HasColumnType("int(11)")
            .HasColumnName("context_window_tokens");
        entity.Property(e => e.Counter)
            .HasComment("计数器")
            .HasColumnType("int(11)")
            .HasColumnName("counter");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DeploymentName)
            .HasMaxLength(100)
            .HasComment("部署名称,Azure需要")
            .HasColumnName("deployment_name");
        entity.Property(e => e.Endpoint)
            .HasMaxLength(100)
            .HasComment("端点")
            .HasColumnName("endpoint");
        entity.Property(e => e.Files)
            .HasComment("支持文件上传")
            .HasColumnName("files");
        entity.Property(e => e.FunctionCall)
            .HasComment("支持函数")
            .HasColumnName("function_call");
        entity.Property(e => e.ImageOutput)
            .HasComment("支持图片输出")
            .HasColumnName("image_output");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublic)
            .HasComment("是否开放给大家使用")
            .HasColumnName("is_public");
        entity.Property(e => e.IsVision)
            .HasComment("支持计算机视觉")
            .HasColumnName("is_vision");
        entity.Property(e => e.Key)
            .HasMaxLength(100)
            .HasComment("密钥")
            .HasColumnName("key");
        entity.Property(e => e.MaxDimension)
            .HasComment("向量的维度")
            .HasColumnType("int(11)")
            .HasColumnName("max_dimension");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("模型名称,gpt-4o")
            .HasColumnName("name");
        entity.Property(e => e.TextOutput)
            .HasDefaultValueSql("'8192'")
            .HasComment("最大文本输出token")
            .HasColumnType("int(11)")
            .HasColumnName("text_output");
        entity.Property(e => e.Title)
            .HasMaxLength(50)
            .HasComment("自定义名模型名称，便于用户选择")
            .HasColumnName("title");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelEntity> modelBuilder);
}
