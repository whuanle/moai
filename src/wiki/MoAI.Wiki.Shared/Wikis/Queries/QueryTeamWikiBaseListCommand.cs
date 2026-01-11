using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 获取团队下的知识库列表.
/// </summary>
public class QueryTeamWikiBaseListCommand : IRequest<IReadOnlyCollection<QueryWikiInfoResponse>>, IModelValidator<QueryTeamWikiBaseListCommand>, IDynamicOrderable
{
    /// <summary>
    /// 限制查询某个团队下的知识库.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 排序，支持 Name、CreateTime、UpdateTime 排序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    private static readonly HashSet<string> allowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "CreateTime",
        "UpdateTime"
    };

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamWikiBaseListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("团队id不正确")
            .GreaterThan(0).WithMessage("团队id不正确");

        validate.RuleFor(x => x.OrderByFields)
            .Must(fields =>
            {
                return fields.All(field => allowedFields.Contains(field.Key));
            })
            .WithMessage("只支持排序 Name, CreateTime, UpdateTime.");
    }
}
