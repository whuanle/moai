using System;
using System.Collections.Generic;
using System.Linq;

namespace MoAI.Database.Seed;

/// <summary>
/// 内置设置项注册表，作为种子数据与首次写入时的初始数据来源.
/// </summary>
public static class SettingDefinitions
{
    /// <summary>
    /// 是否开启知识图谱图数据库.
    /// </summary>
    public const string GraphEnabledKey = "KG_ENABLED";

    /// <summary>
    /// 图数据库连接地址.
    /// </summary>
    public const string GraphUriKey = "KG_URI";

    /// <summary>
    /// 图数据库登录用户名.
    /// </summary>
    public const string GraphUsernameKey = "KG_USERNAME";

    /// <summary>
    /// 图数据库登录密码.
    /// </summary>
    public const string GraphPasswordKey = "KG_PASSWORD";

    /// <summary>
    /// 图数据库方言（memgraph / neo4j）.
    /// </summary>
    public const string GraphDialectKey = "KG_DIALECT";

    /// <summary>
    /// 知识库上传文件大小上限（MB），0 表示不限制.
    /// </summary>
    public const string WikiMaxFileSizeKey = "WIKI_MAX_FILE_SIZE_MB";

    /// <summary>
    /// 每个应用沙箱最大存活时间（秒）.
    /// </summary>
    public const string SandboxMaxTtlKey = "SANDBOX_MAX_TTL_SECONDS";

    /// <summary>
    /// 每个应用沙箱 CPU 限制上限（K8s 数量格式，如 4 或 2000m）.
    /// </summary>
    public const string SandboxMaxCpuKey = "SANDBOX_MAX_CPU";

    /// <summary>
    /// 每个应用沙箱内存限制上限（K8s 数量格式，如 8Gi 或 512Mi）.
    /// </summary>
    public const string SandboxMaxMemoryKey = "SANDBOX_MAX_MEMORY";

    /// <summary>
    /// 网站 Logo（存储中的 ObjectKey，空表示使用默认 Logo）.
    /// </summary>
    public const string SystemLogoKey = "SYSTEM_LOGO";

    /// <summary>
    /// 网站名称（仅前端展示），空表示使用配置文件中的默认名称.
    /// </summary>
    public const string SystemNameKey = "SYSTEM_NAME";

    private static readonly List<SettingDefinition> BackingField = new()
    {
        new SettingDefinition
        {
            Key = GraphEnabledKey,
            Name = "知识图谱",
            Description = "开启后，团队可以使用知识图谱能力；关闭时无需填写连接信息.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = GraphUriKey,
            Name = "图数据库连接地址",
            Description = "图数据库 Bolt 连接地址，例如 neo4j://127.0.0.1:7687 或 bolt://127.0.0.1:7687.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = GraphUsernameKey,
            Name = "图数据库用户名",
            Description = "图数据库登录用户名.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = GraphPasswordKey,
            Name = "图数据库密码",
            Description = "图数据库登录密码.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = GraphDialectKey,
            Name = "图数据库方言",
            Description = "memgraph 或 neo4j，影响内省与索引语句；外部接入 Neo4j 实例时选 neo4j.",
            DefaultValue = "memgraph"
        },
        new SettingDefinition
        {
            Key = WikiMaxFileSizeKey,
            Name = "知识库最大文件大小",
            Description = "知识库上传文档的大小上限（MB），默认 50，0 表示不限制（平台硬上限 1GB）.",
            DefaultValue = "50"
        },
        new SettingDefinition
        {
            Key = SandboxMaxTtlKey,
            Name = "沙箱存活时间上限",
            Description = "每个应用沙箱最大存活时间（秒），团队保存应用配置时不得超出；默认 86400（24 小时），范围 60~604800.",
            DefaultValue = "86400"
        },
        new SettingDefinition
        {
            Key = SandboxMaxCpuKey,
            Name = "沙箱 CPU 上限",
            Description = "每个应用沙箱 CPU 限制上限（K8s 数量格式，如 4 或 2000m），团队保存应用配置时不得超出；默认 4.",
            DefaultValue = "4"
        },
        new SettingDefinition
        {
            Key = SandboxMaxMemoryKey,
            Name = "沙箱内存上限",
            Description = "每个应用沙箱内存限制上限（K8s 数量格式，如 8Gi 或 512Mi），团队保存应用配置时不得超出；默认 8Gi.",
            DefaultValue = "8Gi"
        },
        new SettingDefinition
        {
            Key = SystemLogoKey,
            Name = "网站 Logo",
            Description = "上传后替换全局网站 Logo（侧边栏与登录/注册页），留空使用默认 Logo.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = SystemNameKey,
            Name = "网站名称",
            Description = "仅影响前端展示（侧边栏标题与浏览器标签页），留空使用配置文件默认名称.",
            DefaultValue = string.Empty
        }
    };

    /// <summary>
    /// 全部内置设置项.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> All => BackingField;

    /// <summary>
    /// 根据 key 查找设置项，未找到返回 null.
    /// </summary>
    /// <param name="key">设置项 key.</param>
    /// <returns>返回 <see cref="SettingDefinition"/> 或 null.</returns>
    public static SettingDefinition? Find(string key)
        => BackingField.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
