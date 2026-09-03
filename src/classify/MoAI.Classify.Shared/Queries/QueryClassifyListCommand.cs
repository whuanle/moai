using FluentValidation;
using MediatR;
using MoAI.Classify.Queries.Responses;

namespace MoAI.Classify.Queries;

/// <summary>
/// 查询分类列表.
/// </summary>
public class QueryClassifyListCommand : IRequest<QueryClassifyListCommandResponse>, IModelValidator<QueryClassifyListCommand>
{
    /// <summary>
    /// 分类类型：plugin|app|kb.
    /// </summary>
    public string? Type { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryClassifyListCommand> validate)
    {
        validate.RuleFor(x => x.Type).Must(t => t == null || ClassifyTypes.All.Contains(t)).WithMessage("分类类型不合法，仅支持 plugin|app|kb.");
    }
}
