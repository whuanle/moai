// <copyright file="AiModelConfiguration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// ai模型.
/// </summary>
public partial class AiModelConfiguration : IEntityTypeConfiguration<AiModelEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ai_model", tb => tb.HasComment("ai模型"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.AiModelType)
            .HasMaxLength(20)
            .HasComment("模型类型")
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
            .HasColumnName("public");
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
            .HasMaxLength(100)
            .HasComment("自定义名模型名称，便于用户选择")
            .HasColumnName("title");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.Vision)
            .HasComment("支持计算机视觉")
            .HasColumnType("int(11)")
            .HasColumnName("vision");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelEntity> modelBuilder);
}
