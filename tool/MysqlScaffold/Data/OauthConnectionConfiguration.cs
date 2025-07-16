using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// oauth2.0系统.
/// </summary>
public partial class OauthConnectionConfiguration : IEntityTypeConfiguration<OauthConnectionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OauthConnectionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("oauth_connection", tb => tb.HasComment("oauth2.0系统"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.AuthorizeUrl)
            .HasMaxLength(1000)
            .HasComment("登录跳转地址")
            .HasColumnName("authorize_url");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IconUrl)
            .HasMaxLength(1000)
            .HasComment("图标地址")
            .HasColumnName("icon_url");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Key)
            .HasMaxLength(100)
            .HasComment("应用key")
            .HasColumnName("key");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("认证名称")
            .HasColumnName("name");
        entity.Property(e => e.Provider)
            .HasMaxLength(20)
            .HasComment("提供商")
            .HasColumnName("provider");
        entity.Property(e => e.Secret)
            .HasMaxLength(100)
            .HasComment("密钥")
            .HasColumnName("secret");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.Uuid)
            .HasMaxLength(50)
            .HasComment("uuid")
            .HasColumnName("uuid");
        entity.Property(e => e.WellKnown)
            .HasMaxLength(1000)
            .HasComment("发现端口")
            .HasColumnName("well_known");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<OauthConnectionEntity> modelBuilder);
}
