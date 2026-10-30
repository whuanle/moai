using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 系统级禁用/启用用户（/api/subsys/system/*，系统应用接入 user_write 范围）；
/// 超级管理员账号受保护不可禁用，其余内部用户层级规则（管理员互不可操作）不适用于系统调用方.
/// </summary>
public class SystemSetUserDisableCommand : IRequest<EmptyCommandResponse>, IModelValidator<SystemSetUserDisableCommand>
{
    /// <summary>
    /// 用户 id，由 Controller 从路由参数回填.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemSetUserDisableCommand> validate)
    {
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不正确.");
    }
}
