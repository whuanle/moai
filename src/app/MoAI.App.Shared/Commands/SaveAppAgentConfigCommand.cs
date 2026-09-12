using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 保存 Agent 应用配置（对话模型、允许使用的插件、知识库与系统提示词），需要团队 Admin 及以上角色；
/// 绑定的模型/插件/知识库必须在该团队有权使用的范围内.
/// </summary>
public class SaveAppAgentConfigCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<SaveAppAgentConfigCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 对话使用的模型 id（元素为 ai_model.id 的 uuid）；null 或空 Guid 表示未选择模型.
    /// </summary>
    public Guid? ModelId { get; init; }

    /// <summary>
    /// 系统提示词，最长 4000 字符.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 允许使用的知识库 id 列表（元素为 wiki.id），须属于本团队.
    /// </summary>
    public IReadOnlyCollection<long> WikiIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 允许使用的插件 id 列表（元素为 plugin.id，uuid），须为本团队可访问插件.
    /// </summary>
    public IReadOnlyCollection<Guid> Plugins { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 对话执行参数（JSON 对象，含沙箱等扩展配置）；为空表示不修改已保存的执行参数.
    /// </summary>
    public JsonElement? ExecutionSettings { get; init; }

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
        validate.RuleFor(x => x.WikiIds).NotNull().WithMessage("知识库列表不能为 null.");
        validate.RuleFor(x => x.Plugins).NotNull().WithMessage("插件列表不能为 null.");
    }
}
