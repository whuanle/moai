using System.Text.Json.Serialization;

namespace MoAI.Subsystem.Client.Resources;

/// <summary>接入自身授权快照.</summary>
public sealed class SubsysGrantsResponse
{
    /// <summary>团队授权范围：allTeams/specifiedTeams.</summary>
    [JsonPropertyName("teamScope")]
    public string TeamScope { get; set; } = string.Empty;

    /// <summary>指定授权团队 id 集合.</summary>
    [JsonPropertyName("teamIds")]
    public List<long> TeamIds { get; set; } = [];

    /// <summary>组织授权记录.</summary>
    [JsonPropertyName("orgGrants")]
    public List<SubsysOrgGrantItem> OrgGrants { get; set; } = [];
}

/// <summary>组织授权项.</summary>
public sealed class SubsysOrgGrantItem
{
    /// <summary>组织角色 id.</summary>
    [JsonPropertyName("orgRoleId")]
    public int OrgRoleId { get; set; }

    /// <summary>授权方向：self/up/down.</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

/// <summary>团队成员列表.</summary>
public sealed class SubsysTeamUsersResponse
{
    /// <summary>成员集合.</summary>
    [JsonPropertyName("items")]
    public List<SubsysTeamUserItem> Items { get; set; } = [];
}

/// <summary>团队成员项.</summary>
public sealed class SubsysTeamUserItem
{
    /// <summary>用户 id.</summary>
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    /// <summary>用户名.</summary>
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>昵称.</summary>
    [JsonPropertyName("nickName")]
    public string NickName { get; set; } = string.Empty;

    /// <summary>脱敏邮箱.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>脱敏手机号.</summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>头像 URL.</summary>
    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;

    /// <summary>团队角色：0=Member 1=Admin 2=Owner.</summary>
    [JsonPropertyName("role")]
    public int Role { get; set; }

    /// <summary>加入时间.</summary>
    [JsonPropertyName("joinTime")]
    public DateTimeOffset JoinTime { get; set; }
}
