using System.Text.Json.Serialization;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询当前用户可见的技能选项（系统内置 ∪ 公开 ∪ 指定团队 ∪ 本人个人技能），登录用户可调用.
/// </summary>
public class QuerySkillOptionsCommand : IRequest<QuerySkillOptionsCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 团队 id，0=不限定团队范围.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否包含本人个人技能；应用绑定场景传 false（个人技能不进团队应用配置），用户自选场景传 true.
    /// </summary>
    public bool IncludePersonal { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}
