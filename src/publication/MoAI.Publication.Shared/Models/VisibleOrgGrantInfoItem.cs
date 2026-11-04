using System.Text.Json.Serialization;
using MoAI.Database.Enums;

namespace MoAI.Publication.Models;

/// <summary>
/// 应用上架可视范围组织授权展示项：组织授权项 + 组织名称（列表回显用）.
/// </summary>
public class VisibleOrgGrantInfoItem
{
    /// <summary>
    /// 组织角色 id（org_role.id）.
    /// </summary>
    [JsonPropertyName("orgRoleId")]
    public int OrgRoleId { get; set; }

    /// <summary>
    /// 组织名称.
    /// </summary>
    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// 授权方向：self=仅该组织 up=该组织及以上 down=该组织及以下.
    /// </summary>
    [JsonPropertyName("scope")]
    public OrgGrantScope Scope { get; set; }
}
