using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 组织授权资源类型：哪些资源可按组织线放通，新资源扩枚举接入.
/// </summary>
public enum OrgGrantResourceType
{
    /// <summary>
    /// 应用（Agent/流程应用），主键 Guid.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,

    /// <summary>
    /// 团队知识库，主键 long.
    /// </summary>
    [JsonPropertyName("wiki")]
    Wiki = 1,

    /// <summary>
    /// 团队知识图谱，主键 Guid.
    /// </summary>
    [JsonPropertyName("knowledgeGraph")]
    KnowledgeGraph = 2,

    /// <summary>
    /// 提示词，主键 long.
    /// </summary>
    [JsonPropertyName("prompt")]
    Prompt = 3,

    /// <summary>
    /// 技能，主键 Guid.
    /// </summary>
    [JsonPropertyName("skill")]
    Skill = 4,

    /// <summary>
    /// 子系统（委托 SSO 登录等使用授权），主键 Guid.
    /// </summary>
    [JsonPropertyName("subsystem")]
    Subsystem = 5,
}

/// <summary>
/// 组织授权方向：授权沿组织树扩散的规则，判定基于 path 前缀匹配.
/// </summary>
public enum OrgGrantScope
{
    /// <summary>
    /// 仅该角色本身.
    /// </summary>
    [JsonPropertyName("self")]
    Self = 0,

    /// <summary>
    /// 向上：该角色及其全部祖先（「R 及以上」，如经理及以上可见）.
    /// </summary>
    [JsonPropertyName("up")]
    Up = 1,

    /// <summary>
    /// 向下：该角色及其整棵子树（「R 及以下」，如某部门全员可见）.
    /// </summary>
    [JsonPropertyName("down")]
    Down = 2,
}

/// <summary>
/// 组织授权资源类型解析：路由/查询串中的字符串（枚举名或 JsonPropertyName，忽略大小写）转枚举.
/// </summary>
public static class OrgGrantResourceTypes
{
    private static readonly Dictionary<string, OrgGrantResourceType> NameMap = BuildMap();

    /// <summary>
    /// 尝试解析资源类型字符串（枚举名或 json 名，忽略大小写）.
    /// </summary>
    /// <param name="value">枚举名或 JsonPropertyName 字符串.</param>
    /// <param name="resourceType">解析结果.</param>
    /// <returns>是否解析成功.</returns>
    public static bool TryParse(string? value, out OrgGrantResourceType resourceType)
    {
        resourceType = default;
        return !string.IsNullOrWhiteSpace(value) && NameMap.TryGetValue(value, out resourceType);
    }

    private static Dictionary<string, OrgGrantResourceType> BuildMap()
    {
        var map = new Dictionary<string, OrgGrantResourceType>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in typeof(OrgGrantResourceType).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (OrgGrantResourceType)field.GetValue(null)!;
            map[field.Name] = value;
            var jsonName = field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (!string.IsNullOrEmpty(jsonName))
            {
                map[jsonName] = value;
            }
        }

        return map;
    }
}
