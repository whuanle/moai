using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除知识图谱，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
    }
}
