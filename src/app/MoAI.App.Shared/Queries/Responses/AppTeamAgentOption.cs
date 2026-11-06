namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用绑定的团队智能体选项：配置回显与对话端切换器共用（名称/头像/描述 + 实时启用状态 + 偏好模型）.
/// </summary>
public class AppTeamAgentOption
{
    /// <summary>
    /// 团队智能体 id（team_agent.id）.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 智能体标识 key（团队内唯一，小写蛇形，如 plan/build）.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 智能体名称（展示名，可中文）.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 智能体描述，空串=未填写.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 头像文件 objectKey，空串=未设置（前端经 resolveStorageUrl 转可访问地址）.
    /// </summary>
    public string AvatarObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用（配置回显用：绑定后又被停用的智能体标记 false，前端提示重新选择）.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 主智能体声明的插件依赖（弱依赖：前端对比应用绑定提示一键补齐）.
    /// </summary>
    public IReadOnlyList<Guid> MainPluginIds { get; set; } = new List<Guid>();

    /// <summary>
    /// 主智能体声明的知识库依赖（弱依赖：同上）.
    /// </summary>
    public IReadOnlyList<long> MainWikiIds { get; set; } = new List<long>();

    /// <summary>
    /// 主智能体声明的知识图谱依赖（弱依赖：同上）.
    /// </summary>
    public IReadOnlyList<long> MainGraphIds { get; set; } = new List<long>();
}
