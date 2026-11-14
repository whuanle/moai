using System.Text.Json.Serialization;
using MoAI.Database.Enums;

namespace MoAI.Publication.Models;

/// <summary>
/// 应用上架可视范围组织授权项：一个组织角色一条方向规则（语义与组织授权 OrgGrantItem 一致）.
/// </summary>
public class VisibleOrgGrantItem
{
    /// <summary>
    /// 组织角色 id（org_role.id）.
    /// </summary>
    [JsonPropertyName("orgRoleId")]
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 授权方向：self=仅该组织 up=该组织及以上 down=该组织及以下.
    /// </summary>
    [JsonPropertyName("scope")]
    public OrgGrantScope Scope { get; init; }
}
