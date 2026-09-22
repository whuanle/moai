using System.Text.Json;
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
    /// 绑定的知识图谱 id 列表（元素为 knowledge_graph.id）.
    /// </summary>
    public IReadOnlyList<long> GraphIds { get; set; } = new List<long>();

    /// <summary>
    /// 绑定的插件 id 列表（元素为 plugin.id，uuid）.
    /// </summary>
    public IReadOnlyList<Guid> Plugins { get; set; } = new List<Guid>();

    /// <summary>
    /// 绑定为工具的流程应用 id 列表（元素为 app.id，uuid，本团队已发布流程应用）.
    /// </summary>
    public IReadOnlyList<Guid> WorkflowApps { get; set; } = new List<Guid>();

    /// <summary>
    /// 应用默认使用的技能 id 列表（元素为 skill.id，uuid），用户可在应用设置中取消勾选.
    /// </summary>
    public IReadOnlyList<Guid> Skills { get; set; } = new List<Guid>();

    /// <summary>
    /// 对话执行参数（JSON 对象，含沙箱等扩展配置）.
    /// </summary>
    public JsonElement ExecutionSettings { get; set; }

    /// <summary>
    /// 对话开场白，未配置时为空串.
    /// </summary>
    public string OpeningStatement { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用对话开场白；启用且内容非空时，新会话开始时展示.
    /// </summary>
    public bool OpeningStatementEnabled { get; set; }

    /// <summary>
    /// 快捷输入列表（管理员配置，用户在对话欢迎态点击即发送），未配置为空列表.
    /// </summary>
    public IReadOnlyList<string> QuickInputs { get; set; } = new List<string>();

    /// <summary>
    /// 配置状态：0=草稿有未发布变更 1=当前草稿与已发布一致；已发布应用 status=0 时线上仍按发布快照执行.
    /// </summary>
    public short Status { get; set; }

    /// <summary>
    /// 我在所属团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }
}
