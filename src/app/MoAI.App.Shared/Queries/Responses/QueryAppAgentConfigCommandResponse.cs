using MoAI.Database.Enums;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// Agent 应用配置响应.
/// </summary>
public class QueryAppAgentConfigCommandResponse
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 应用类型：Agent 应用=0，流程应用=1.
    /// </summary>
    public AppType AppType { get; set; }

    /// <summary>
    /// 系统提示词，未配置时为空串.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 对话使用的模型 id（uuid）；模型选择未开放时为空 Guid.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 绑定的知识库 id 列表（元素为 wiki.id）.
    /// </summary>
    public IReadOnlyList<long> WikiIds { get; set; } = new List<long>();

    /// <summary>
    /// 绑定的插件 id 列表（元素为 plugin.id，uuid）.
    /// </summary>
    public IReadOnlyList<Guid> Plugins { get; set; } = new List<Guid>();

    /// <summary>
    /// 我在所属团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }
}
