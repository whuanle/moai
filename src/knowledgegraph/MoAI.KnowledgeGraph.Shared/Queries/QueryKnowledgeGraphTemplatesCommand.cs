using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询内置模板目录.
/// </summary>
public class QueryKnowledgeGraphTemplatesCommand : IRequest<QueryKnowledgeGraphTemplatesCommandResponse>, IModelValidator<QueryKnowledgeGraphTemplatesCommand>
{
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphTemplatesCommand> validate)
    {
    }
}
