using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 删除知识库文档（软删除），需要团队 Admin 及以上角色（内容删除为破坏性操作）.
/// </summary>
public class DeleteWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiDocumentCommand>
{
    /// <summary>
    /// 文档 id，由 Controller 从路由参数回填.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteWikiDocumentCommand> validate)
    {
        // DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，实体校验在 Handler 层完成.
    }
}
