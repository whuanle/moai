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
