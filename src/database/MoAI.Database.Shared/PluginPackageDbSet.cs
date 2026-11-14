using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>在线插件包表的上下文扩展.</summary>
public partial class DatabaseContext
{
    /// <summary>MoPlatform 在线插件安装记录.</summary>
    public virtual DbSet<PluginPackageEntity> PluginPackages { get; set; } = null!;
}
