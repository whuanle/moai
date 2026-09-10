using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除实体类型，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphEntityTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphEntityTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphEntityTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
    }
}
