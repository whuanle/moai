using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Variable.Commands;

/// <summary>
/// 删除团队变量，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteVariableCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteVariableCommand>
{
    /// <summary>
    /// 变量 id，由 Controller 从路由参数回填.
    /// </summary>
    public long VariableId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteVariableCommand> validate)
    {
        // VariableId 由 Controller 从路由参数回填，自动验证发生在回填之前，实体校验在 Handler 层完成.
    }
}
