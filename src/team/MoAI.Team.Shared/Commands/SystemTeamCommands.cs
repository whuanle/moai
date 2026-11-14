using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Team.Commands;

/// <summary>
/// 系统级创建团队（/api/subsys/system/*，系统应用接入 team_write 范围）：仅建团队，不添加成员（成员经成员接口推送）.
/// </summary>
public class SystemCreateTeamCommand : IRequest<SimpleLong>, IModelValidator<SystemCreateTeamCommand>
{
    /// <summary>
    /// 团队名称（全局唯一）.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队简介，可选.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemCreateTeamCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("团队名称不能为空.").MaximumLength(50).WithMessage("团队名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("团队简介最长 255 个字符.");
    }
}

/// <summary>
/// 系统级添加/更新团队成员（/api/subsys/system/*，系统应用接入 team_write 范围）：
/// 幂等 upsert 语义，已在本团队时更新角色，适配 ERP 等系统的重复推送.
/// </summary>
public class SystemAddTeamUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<SystemAddTeamUserCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 被添加的用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 授予的角色，仅支持 Admin/Member（Owner 转让属人工操作，不经系统接口）.
    /// </summary>
    public TeamRole Role { get; init; } = TeamRole.Member;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemAddTeamUserCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不正确.");
        validate.RuleFor(x => x.Role).Must(x => x == TeamRole.Admin || x == TeamRole.Member).WithMessage("角色只能为 Admin 或 Member.");
    }
}

/// <summary>
/// 系统级移除团队成员（/api/subsys/system/*，系统应用接入 team_write 范围）；团队 Owner 不可经系统接口移除.
/// </summary>
public class SystemRemoveTeamUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<SystemRemoveTeamUserCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 目标成员用户 id，由 Controller 从路由参数回填.
    /// </summary>
    public long UserId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemRemoveTeamUserCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不正确.");
    }
}
