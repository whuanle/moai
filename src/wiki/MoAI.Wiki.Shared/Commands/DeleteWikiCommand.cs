using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 删除知识库（软删除），需要团队 Admin 及以上角色.
/// </summary>
public class DeleteWikiCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteWikiCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，实体校验在 Handler 层完成.
    }
}
