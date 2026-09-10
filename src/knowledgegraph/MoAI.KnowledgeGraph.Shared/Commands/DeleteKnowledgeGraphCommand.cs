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
    public long KgId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
    }
}
