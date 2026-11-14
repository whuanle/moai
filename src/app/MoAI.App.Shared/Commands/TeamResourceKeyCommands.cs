using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 生成团队资源 key（get-or-create）：同一（团队, 资源类型, 资源实例）已有 key 时原样返回；
/// 一个资源一把 key（应用 ACP/A2A 共用其应用的 key）；资源未开启对应对外功能（应用 ACP/A2A、知识库/知识图谱 MCP）时拒绝生成；需要团队 Admin 及以上角色.
/// </summary>
public class CreateTeamResourceKeyCommand : IRequest<CreateTeamResourceKeyCommandResponse>, IUserIdContext, IModelValidator<CreateTeamResourceKeyCommand>
{
    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 资源类型：app=团队应用（ACP/A2A 共用一把）/ wiki=知识库 / knowledge_graph=知识图谱.
    /// </summary>
    public TeamResourceType ResourceType { get; init; }

    /// <summary>
    /// 资源 id（字符串形态）：app 行传应用 Guid、wiki 行传 wikiId、knowledge_graph 行传 kgId.
    /// </summary>
    public string ResourceId { get; init; } = default!;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateTeamResourceKeyCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.ResourceType).IsInEnum().WithMessage("资源类型不正确.");
        validate.RuleFor(x => x.ResourceId).NotEmpty().WithMessage("请指定资源 id.").MaximumLength(64).WithMessage("资源 id 不正确.");
    }
}

/// <summary>
/// 重置团队资源 key：同表行重新生成 key 值（行 id 不变，直连身份延续），旧 key 立即失效；原文仅此一次返回；需要团队 Admin 及以上角色.
/// key 生命周期只有「开启功能时自动生成/复用 + 重置」，不支持删除（吊销手段=重置或关闭资源功能开关）.
/// </summary>
public class ResetTeamResourceKeyCommand : IRequest<ResetTeamResourceKeyCommandResponse>, IUserIdContext, IModelValidator<ResetTeamResourceKeyCommand>
{
    /// <summary>
    /// 资源 key 行 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid TeamResourceKeyId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetTeamResourceKeyCommand> validate)
    {
        validate.RuleFor(x => x.TeamResourceKeyId).NotEmpty().WithMessage("资源 key id 不正确.");
    }
}
