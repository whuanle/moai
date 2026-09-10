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
    /// 是否开启 Neo4j 知识图谱.
    /// </summary>
    public const string Neo4jEnabledKey = "OPEN_NEO4J";

    /// <summary>
    /// Neo4j 连接地址.
    /// </summary>
    public const string Neo4jUriKey = "NEO4J_URI";

    /// <summary>
    /// Neo4j 用户名.
    /// </summary>
    public const string Neo4jUsernameKey = "NEO4J_USERNAME";

    /// <summary>
    /// Neo4j 密码.
    /// </summary>
    public const string Neo4jPasswordKey = "NEO4J_PASSWORD";

    private static readonly List<SettingDefinition> BackingField = new()
    {
        new SettingDefinition
        {
            Key = Neo4jEnabledKey,
            Name = "Neo4j 知识图谱",
            Description = "开启后，知识库可以使用知识图谱能力；关闭时无需填写连接信息.",
            DefaultValue = "false"
        },
        new SettingDefinition
        {
            Key = Neo4jUriKey,
            Name = "Neo4j 连接地址",
            Description = "Neo4j 数据库连接地址，例如 neo4j://127.0.0.1:7687.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = Neo4jUsernameKey,
            Name = "Neo4j 用户名",
            Description = "Neo4j 数据库登录用户名.",
            DefaultValue = string.Empty
        },
        new SettingDefinition
        {
            Key = Neo4jPasswordKey,
            Name = "Neo4j 密码",
            Description = "Neo4j 数据库登录密码.",
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
