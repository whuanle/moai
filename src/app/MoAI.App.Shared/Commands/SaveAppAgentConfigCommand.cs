using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 保存 Agent 应用配置（对话模型、允许使用的插件、知识库、知识图谱与系统提示词），需要团队 Admin 及以上角色；
/// 绑定的模型/插件/知识库/知识图谱必须在该团队有权使用的范围内.
/// </summary>
public class SaveAppAgentConfigCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<SaveAppAgentConfigCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 对话使用的模型 id（元素为 ai_model.id 的 uuid）；Agent 应用必填（不存在「默认模型」回退语义），流程应用忽略.
    /// </summary>
    public Guid? ModelId { get; init; }

    /// <summary>
    /// 开放给用户选择的对话模型 id 列表（元素为 ai_model.id 的 uuid），须包含 ModelId（默认模型），
    /// 最多 20 个，每个均须该团队可用；null 或空时归一为仅默认模型（单模型应用，存量客户端兼容），流程应用忽略.
    /// </summary>
    public IReadOnlyCollection<Guid>? ModelIds { get; init; }

    /// <summary>
    /// 系统提示词，最长 4000 字符；Agent 应用必填非空，流程应用忽略.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 允许使用的知识库 id 列表（元素为 wiki.id），须属于本团队.
    /// </summary>
    public IReadOnlyCollection<long> WikiIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 允许使用的知识图谱 id 列表（元素为 knowledge_graph.id），须属于本团队且为平台托管图（接入图不支持）.
    /// </summary>
    public IReadOnlyCollection<long> GraphIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 允许使用的插件 id 列表（元素为 plugin.id，uuid），须为本团队可访问插件.
    /// </summary>
    public IReadOnlyCollection<Guid> Plugins { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 绑定为工具的流程应用 id 列表（元素为 app.id，uuid），须为本团队已发布流程应用；
    /// null 表示保持已保存的流程应用绑定不变.
    /// </summary>
    public IReadOnlyCollection<Guid>? WorkflowApps { get; init; }

    /// <summary>
    /// 绑定为工具的 Agent 应用 id 列表（元素为 app.id，uuid），须为本团队已发布内部 Agent 应用且不含自身；
    /// null 表示保持已保存的 Agent 应用绑定不变.
    /// </summary>
    public IReadOnlyCollection<Guid>? AgentApps { get; init; }

    /// <summary>
    /// 应用默认使用的技能 id 列表（元素为 skill.id，uuid），须为启用中的系统内置/市场公开/本团队技能；
    /// 用户进入对话后可在应用设置中取消勾选；null 表示保持已保存的技能不变.
    /// </summary>
    public IReadOnlyCollection<Guid>? Skills { get; init; }

    /// <summary>
    /// 对话开场白，最长 4000 字符；新会话开始时展示给用户，不参与模型上下文.
    /// </summary>
    public string OpeningStatement { get; init; } = string.Empty;

    /// <summary>
    /// 是否启用对话开场白；启用且内容非空时生效.
    /// </summary>
    public bool OpeningStatementEnabled { get; init; }

    /// <summary>
    /// 快捷输入列表（管理员配置，用户在对话欢迎态点击即发送），最多 10 条、每条最长 200 字符；
    /// null 表示保持已保存的快捷输入不变.
    /// </summary>
    public IReadOnlyCollection<string>? QuickInputs { get; init; }

    /// <summary>
    /// 对话执行参数（JSON 对象，含沙箱等扩展配置）；为空表示不修改已保存的执行参数.
    /// </summary>
    public JsonElement? ExecutionSettings { get; init; }

    /// <summary>
    /// 绑定的团队智能体 id 列表（元素为 team_agent.id 的 uuid），须为本团队已启用智能体，最多 20 个；
    /// 绑定后须开启子代理协作（executionSettings.subAgent.enabled）；null 表示保持已保存的绑定不变.
    /// </summary>
    public IReadOnlyCollection<Guid>? TeamAgentIds { get; init; }

    /// <summary>
    /// 默认团队智能体 id，须在 TeamAgentIds 内；空=取列表第一个；列表为空时忽略.
    /// </summary>
    public Guid? DefaultTeamAgentId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveAppAgentConfigCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Prompt).MaximumLength(4000).WithMessage("系统提示词最长 4000 个字符.");
        validate.RuleFor(x => x.OpeningStatement).MaximumLength(4000).WithMessage("对话开场白最长 4000 个字符.");
        validate.RuleFor(x => x.WikiIds).NotNull().WithMessage("知识库列表不能为 null.");
        validate.RuleFor(x => x.GraphIds).NotNull().WithMessage("知识图谱列表不能为 null.");
        validate.RuleFor(x => x.Plugins).NotNull().WithMessage("插件列表不能为 null.");
        validate.RuleFor(x => x.QuickInputs).Must(list => list == null || list.Count <= 10)
            .WithMessage("快捷输入最多 10 条.");
        validate.RuleFor(x => x.QuickInputs).Must(list => list?.All(x => x.Length <= 200) ?? true)
            .WithMessage("快捷输入每条最长 200 个字符.");
        validate.RuleFor(x => x.TeamAgentIds).Must(list => list == null || list.Count <= 20)
            .WithMessage("绑定的团队智能体最多 20 个.");
        validate.RuleFor(x => x.TeamAgentIds).Must(list => list == null || list.Select(x => x).Distinct().Count() == list.Count)
            .WithMessage("绑定的团队智能体不能重复.");
    }
}
