using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.TeamAgent.Models;

namespace MoAI.TeamAgent.Commands;

/// <summary>
/// 更新团队智能体（整文档替换，含子智能体列表），需要团队 Admin 及以上角色.
/// </summary>
public class UpdateTeamAgentCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamAgentCommand>, IUserIdContext
{
    /// <summary>
    /// 智能体 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AgentId { get; init; }

    /// <summary>
    /// 智能体名称（展示名，可中文），团队内唯一；标识 key 创建后不可变更，不在更新字段内.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 智能体描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否启用，停用后不可被应用绑定与运行.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 主智能体系统提示词.
    /// </summary>
    public string MainPrompt { get; init; } = default!;

    /// <summary>
    /// 主智能体插件依赖（团队可用插件 id 列表）.
    /// </summary>
    public IReadOnlyList<Guid> MainPluginIds { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 主智能体知识库依赖（本团队知识库 id 列表）.
    /// </summary>
    public IReadOnlyList<long> MainWikiIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 主智能体知识图谱依赖（本团队托管图谱 id 列表）.
    /// </summary>
    public IReadOnlyList<long> MainGraphIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 子智能体列表，最多 20 个.
    /// </summary>
    public IReadOnlyList<TeamAgentSubItem> Subs { get; init; } = Array.Empty<TeamAgentSubItem>();

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamAgentCommand> validate)
    {
        // AgentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段；key 创建后不可变更.
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("智能体名称不能为空.").MaximumLength(50).WithMessage("智能体名称最长 50 个字符.")
            .Matches("^[\u4e00-\u9fa5a-zA-Z0-9_]+$").WithMessage("智能体名称仅允许中文、英文、数字和下划线.");
        validate.RuleFor(x => x.Description).MaximumLength(500).WithMessage("智能体描述最长 500 个字符.");
        validate.RuleFor(x => x.MainPrompt).NotEmpty().WithMessage("主智能体提示词不能为空.").MaximumLength(50000).WithMessage("主智能体提示词最长 50000 个字符.");
        validate.RuleFor(x => x.Subs.Count).LessThanOrEqualTo(20).WithMessage("子智能体最多 20 个.");

        validate.RuleForEach(x => x.Subs).ChildRules(sub =>
        {
            sub.RuleFor(x => x.Key).NotEmpty().WithMessage("子智能体标识不能为空.")
                .Matches("^[a-z][a-z0-9_]{0,29}$").WithMessage("子智能体标识仅允许小写字母开头，包含小写字母/数字/下划线，最长 30.");
            sub.RuleFor(x => x.Name).NotEmpty().WithMessage("子智能体名称不能为空.")
                .MaximumLength(50).WithMessage("子智能体名称最长 50 个字符.")
                .Matches("^[\u4e00-\u9fa5a-zA-Z0-9_]+$").WithMessage("子智能体名称仅允许中文、英文、数字和下划线.");
            sub.RuleFor(x => x.Description).NotEmpty().WithMessage("子智能体描述不能为空，它是主智能体判断何时派发的依据.")
                .MaximumLength(500).WithMessage("子智能体描述最长 500 个字符.");
            sub.RuleFor(x => x.Prompt).NotEmpty().WithMessage("子智能体提示词不能为空.")
                .MaximumLength(30000).WithMessage("子智能体提示词最长 30000 个字符.");
        });
    }
}
