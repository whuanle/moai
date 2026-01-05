using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询模型列表.
/// </summary>
public class QueryAiModelListCommand : IRequest<QueryAiModelListCommandResponse>, IDynamicOrderable, IModelValidator<QueryAiModelListCommand>
{
    /// <summary>
    /// AI 模型类型.
    /// </summary>
    public AiProvider? Provider { get; init; }

    /// <summary>
    /// Ai 模型类型.
    /// </summary>
    public AiModelType? AiModelType { get; init; }

    /// <summary>
    /// 公开使用.
    /// </summary>
    public bool? IsPublic { get; init; }

    /// <summary>
    /// 可以使用 name、 title、provider、aiModelType 字段进行排序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    private static readonly HashSet<string> AllowedOrderByFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AiNotKeyEndpoint.Name),
        nameof(AiNotKeyEndpoint.Title),
        nameof(AiNotKeyEndpoint.Provider),
        nameof(AiNotKeyEndpoint.AiModelType)
    };

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAiModelListCommand> validate)
    {
        validate.RuleFor(x => x.OrderByFields)
            .Must(fields =>
            {
                return fields.All(field => AllowedOrderByFields.Contains(field.Key));
            })
            .WithMessage("不支持这些排序字段.");
    }
}
