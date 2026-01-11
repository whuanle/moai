using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 获取知识库列表.
/// </summary>
public class QueryWikiBaseListCommand : IUserIdContext, IRequest<IReadOnlyCollection<QueryWikiInfoResponse>>, IDynamicOrderable, IModelValidator<QueryWikiBaseListCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 只查询私有知识库.
    /// </summary>
    public bool? IsOwn { get; init; }

    /// <summary>
    /// 这个知识库所在的团队，自己可以参与协作的，默认 true.
    /// </summary>
    public bool? IsInTeam { get; init; }

    /// <summary>
    /// 排序，支持 Name、CreateTime、UpdateTime 排序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    private static readonly HashSet<string> allowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(QueryWikiInfoResponse.Name),
        nameof(QueryWikiInfoResponse.CreateTime),
        nameof(QueryWikiInfoResponse.UpdateTime)
    };

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiBaseListCommand> validate)
    {
        validate.RuleFor(x => x.OrderByFields)
            .Must(fields =>
            {
                return fields.All(field => allowedFields.Contains(field.Key));
            })
            .WithMessage("只支持排序 Name, CreateTime, UpdateTime.");
    }
}