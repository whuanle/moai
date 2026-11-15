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

/// <summary>接入自省（含事件通道自描述）.</summary>
public sealed class SubsysInfoResponse
{
    /// <summary>接入 id.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>接入标识（appid）.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>接入名称.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>接入类型：subsystem/app.</summary>
    [JsonPropertyName("appType")]
    public string AppType { get; set; } = string.Empty;

    /// <summary>接入状态：enabled/disabled.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>团队授权范围：allTeams/specifiedTeams.</summary>
    [JsonPropertyName("teamScope")]
    public string TeamScope { get; set; } = string.Empty;

    /// <summary>已授权团队数.</summary>
    [JsonPropertyName("authorizedTeamCount")]
    public int AuthorizedTeamCount { get; set; }

    /// <summary>委托 SSO 登录回调地址列表.</summary>
    [JsonPropertyName("callbackUrls")]
    public List<string> CallbackUrls { get; set; } = [];

    /// <summary>是否在线（心跳窗口内有存活实例）.</summary>
    [JsonPropertyName("online")]
    public bool Online { get; set; }

    /// <summary>在线实例数.</summary>
    [JsonPropertyName("onlineInstances")]
    public int OnlineInstances { get; set; }

    /// <summary>事件通道自描述.</summary>
    [JsonPropertyName("events")]
    public SubsysEventChannelInfo Events { get; set; } = new();
}

/// <summary>事件通道自描述（服务端 info/token 响应回显）.</summary>
public sealed class SubsysEventChannelInfo
{
    /// <summary>SignalR 事件 Hub 路径.</summary>
    [JsonPropertyName("hubPath")]
    public string HubPath { get; set; } = "/hub/subsys";

    /// <summary>REST 事件补发路径.</summary>
    [JsonPropertyName("restPath")]
    public string RestPath { get; set; } = "/api/subsys/events";

    /// <summary>事件目录路径.</summary>
    [JsonPropertyName("catalogPath")]
    public string CatalogPath { get; set; } = "/api/subsys/events/catalog";

    /// <summary>事件信封版本.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>补发索引保留天数.</summary>
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; }

    /// <summary>单次补发上限.</summary>
    [JsonPropertyName("maxReplay")]
    public int MaxReplay { get; set; }
}

/// <summary>心跳响应.</summary>
public sealed class SubsysHeartbeatResponse
{
    /// <summary>服务端 UTC 时间.</summary>
    [JsonPropertyName("serverTime")]
    public DateTimeOffset ServerTime { get; set; }

    /// <summary>本次心跳计入后的在线实例数.</summary>
    [JsonPropertyName("onlineInstances")]
    public int OnlineInstances { get; set; }

    /// <summary>在线判定窗口（秒）.</summary>
    [JsonPropertyName("windowSeconds")]
    public int WindowSeconds { get; set; }

    /// <summary>建议心跳间隔（秒）.</summary>
    [JsonPropertyName("recommendedIntervalSeconds")]
    public int RecommendedIntervalSeconds { get; set; }
}

/// <summary>事件目录响应.</summary>
public sealed class SubsysEventCatalogResponse
{
    /// <summary>事件信封版本.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>补发索引保留天数.</summary>
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; }

    /// <summary>事件目录.</summary>
    [JsonPropertyName("items")]
    public List<SubsysEventDescriptor> Items { get; set; } = [];
}

/// <summary>事件目录项.</summary>
public sealed class SubsysEventDescriptor
{
    /// <summary>事件名.</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>受众：all/target/control.</summary>
    [JsonPropertyName("audience")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>载荷字段名.</summary>
    [JsonPropertyName("dataFields")]
    public List<string> DataFields { get; set; } = [];
}
