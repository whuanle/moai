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
/// 企业微信应用，一个企业微信管理端自建应用（CorpId + AgentId + Secret）对应一条连接，
/// 消息经回调 URL 统一接收后按绑定转发.
/// </summary>
internal partial class WeixinWorkAppConfiguration : IEntityTypeConfiguration<WeixinWorkAppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WeixinWorkAppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("weixinwork_app_pkey");

        entity.ToTable("weixinwork_app", tb => tb.HasComment("企业微信应用，一个企业微信管理端自建应用（CorpId + AgentId + Secret）对应一条连接，消息经回调 URL 统一接收后按绑定转发"));

        // 同一企业同一自建应用全局只允许一条连接，避免同一应用多处接入互抢回调
        entity.HasIndex(e => new { e.CorpId, e.AgentId }, "idx_weixinwork_app_corp_agent_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_weixinwork_app_team_id_index");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AgentId)
            .HasComment("企业微信自建应用 AgentId")
            .HasColumnName("agent_id");
        entity.Property(e => e.ApiEndpoint)
            .HasMaxLength(200)
            .HasDefaultValueSql("''::character varying")
            .HasComment("API 地址覆盖（默认 https://qyapi.weixin.qq.com），供本地桩联调或代理使用，留空走官方")
            .HasColumnName("api_endpoint");
        entity.Property(e => e.CorpId)
            .HasMaxLength(64)
            .HasComment("企业微信企业 ID（管理后台 我的企业 → 企业信息），与 AgentId 联合全局唯一")
            .HasColumnName("corp_id");
        entity.Property(e => e.CorpSecret)
            .HasMaxLength(128)
            .HasComment("企业微信自建应用 Secret（应用管理 → 自建应用 → Secret）")
            .HasColumnName("corp_secret");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.EncodingAesKey)
            .HasMaxLength(64)
            .HasComment("接收消息回调配置 EncodingAESKey（43 字符），用于回调消息加解密")
            .HasColumnName("encoding_aes_key");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("禁用，禁用后回调推送不再处理")
            .HasColumnName("is_disable");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("连接名称，团队内唯一")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.Token)
            .HasMaxLength(128)
            .HasComment("接收消息回调配置 Token（管理后台 接收消息 → 设置 API 接收），用于回调签名校验")
            .HasColumnName("token");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WeixinWorkAppEntity> modelBuilder);
}
