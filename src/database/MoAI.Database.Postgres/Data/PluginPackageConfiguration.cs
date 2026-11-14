using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>在线插件包表结构.</summary>
internal sealed class PluginPackageConfiguration : IEntityTypeConfiguration<PluginPackageEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginPackageEntity> builder)
    {
        builder.HasKey(x => x.Id).HasName("plugin_package_pkey");
        builder.ToTable("plugin_package", table => table.HasComment("MoPlatform 在线插件包安装快照"));
        builder.HasIndex(x => new { x.PluginKey, x.IsDeleted }).IsUnique().HasDatabaseName("idx_plugin_package_key_deleted_uindex");
        builder.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id").HasComment("主键");
        builder.Property(x => x.PlatformPluginId).HasColumnName("platform_plugin_id").HasComment("MoPlatform 插件 id");
        builder.Property(x => x.PlatformVersionId).HasColumnName("platform_version_id").HasComment("MoPlatform 当前包版本 id");
        builder.Property(x => x.PluginKey).HasMaxLength(64).HasColumnName("plugin_key").HasComment("插件唯一 key");
        builder.Property(x => x.Version).HasMaxLength(32).HasColumnName("version").HasComment("插件包版本");
        builder.Property(x => x.PackageHash).HasMaxLength(64).HasColumnName("package_hash").HasComment("插件包 SHA-256 十六进制摘要");
        builder.Property(x => x.EntryAssembly).HasMaxLength(512).HasColumnName("entry_assembly").HasComment("插件入口程序集相对路径");
        builder.Property(x => x.ManifestJson).HasColumnType("text").HasColumnName("manifest_json").HasComment("MoPlatform 兼容性清单 JSON");
        builder.Property(x => x.MetadataJson).HasColumnType("text").HasColumnName("metadata_json").HasComment("插件本地元数据快照 JSON");
        builder.Property(x => x.Name).HasMaxLength(100).HasColumnName("name").HasComment("插件名称");
        builder.Property(x => x.Summary).HasMaxLength(512).HasColumnName("summary").HasComment("插件简短说明");
        builder.Property(x => x.Description).HasColumnType("text").HasColumnName("description").HasComment("插件详细描述");
        builder.Property(x => x.AvatarPath).HasMaxLength(255).HasDefaultValue(string.Empty).HasColumnName("avatar_path").HasComment("MoAI OSS 插件图标对象 key");
        builder.Property(x => x.Enabled).HasDefaultValue(true).HasColumnName("enabled").HasComment("是否上架启用；下架不删除包也不卸载程序集");
        builder.Property(x => x.CreateUserId).HasDefaultValue(0L).HasColumnName("create_user_id").HasComment("创建人");
        builder.Property(x => x.CreateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasColumnName("create_time").HasComment("创建时间");
        builder.Property(x => x.UpdateUserId).HasDefaultValue(0L).HasColumnName("update_user_id").HasComment("最后修改人");
        builder.Property(x => x.UpdateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasColumnName("update_time").HasComment("更新时间");
        builder.Property(x => x.IsDeleted).HasDefaultValue(0L).HasColumnName("is_deleted").HasComment("软删除");
    }
}
