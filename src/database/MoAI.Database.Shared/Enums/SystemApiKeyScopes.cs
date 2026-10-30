using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 系统资源功能范围位标记，与 access_app.scopes 位或组合存储（仅系统级接入使用）；
/// 位空间与 <see cref="TeamApiKeyScopes"/>（低位 1..1024）隔离，系统资源占用 1&lt;&lt;16 起的高位；
/// 系统级接入（如 ERP 同步组织角色）可操作系统资源的授权范围 = 勾选的功能块.
/// </summary>
[Flags]
public enum SystemApiKeyScopes
{
    /// <summary>
    /// 无任何范围（创建时至少一个）.
    /// </summary>
    None = 0,

    /// <summary>
    /// 组织架构读：查询组织/角色树与成员关系（协作面系统资源接口）.
    /// </summary>
    OrgRead = 1 << 16,

    /// <summary>
    /// 组织架构写：创建/更新组织角色与成员绑定（ERP 同步入口）.
    /// </summary>
    OrgWrite = 1 << 17,

    /// <summary>
    /// 用户治理读：查询平台用户基本信息.
    /// </summary>
    UserRead = 1 << 18,

    /// <summary>
    /// 用户治理写：创建/更新/禁用平台用户（ERP 同步入口）.
    /// </summary>
    UserWrite = 1 << 19,

    /// <summary>
    /// 团队读：查询平台团队清单.
    /// </summary>
    TeamRead = 1 << 20,

    /// <summary>
    /// 团队写：创建/更新团队与成员管理（ERP 同步入口）.
    /// </summary>
    TeamWrite = 1 << 21,
}

/// <summary>
/// <see cref="SystemApiKeyScopes"/> 与对外 scope 代码（小写下划线串）互转.
/// </summary>
public static class SystemApiKeyScopeCodes
{
    /// <summary>
    /// 全部可选范围.
    /// </summary>
    public const SystemApiKeyScopes All = SystemApiKeyScopes.OrgRead | SystemApiKeyScopes.OrgWrite | SystemApiKeyScopes.UserRead | SystemApiKeyScopes.UserWrite | SystemApiKeyScopes.TeamRead | SystemApiKeyScopes.TeamWrite;

    /// <summary>
    /// 代码 → 枚举映射表（顺序即展示顺序）.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SystemApiKeyScopes> CodeMap = new Dictionary<string, SystemApiKeyScopes>
    {
        ["org_read"] = SystemApiKeyScopes.OrgRead,
        ["org_write"] = SystemApiKeyScopes.OrgWrite,
        ["user_read"] = SystemApiKeyScopes.UserRead,
        ["user_write"] = SystemApiKeyScopes.UserWrite,
        ["team_read"] = SystemApiKeyScopes.TeamRead,
        ["team_write"] = SystemApiKeyScopes.TeamWrite,
    };

    /// <summary>
    /// 解析 scope 代码列表：未知代码或空列表返回 false.
    /// </summary>
    /// <param name="codes">代码列表.</param>
    /// <param name="scopes">位或组合结果.</param>
    /// <returns>是否全部合法且非空.</returns>
    public static bool TryParseCodes(IEnumerable<string> codes, out SystemApiKeyScopes scopes)
    {
        scopes = SystemApiKeyScopes.None;
        var result = SystemApiKeyScopes.None;
        foreach (var code in codes)
        {
            if (!CodeMap.TryGetValue(code, out var parsed))
            {
                return false;
            }

            if (result.HasFlag(parsed))
            {
                return false;
            }

            result |= parsed;
        }

        if (result == SystemApiKeyScopes.None)
        {
            return false;
        }

        scopes = result;
        return true;
    }

    /// <summary>
    /// 枚举位 → 代码列表.
    /// </summary>
    /// <param name="scopes">位或组合.</param>
    /// <returns>代码列表.</returns>
    public static IReadOnlyList<string> ToCodes(SystemApiKeyScopes scopes)
    {
        var codes = new List<string>();
        foreach (var (code, flag) in CodeMap)
        {
            if (scopes.HasFlag(flag))
            {
                codes.Add(code);
            }
        }

        return codes;
    }
}
