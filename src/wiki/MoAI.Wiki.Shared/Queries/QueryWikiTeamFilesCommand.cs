using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 跨知识库按文件名搜索团队内的文档，仅团队成员可访问.
/// </summary>
public class QueryWikiTeamFilesCommand : IRequest<QueryWikiTeamFilesCommandResponse>, IModelValidator<QueryWikiTeamFilesCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 文件名关键字（子串匹配，不区分大小写由数据库排序规则决定）.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 返回条数上限，默认 50.
    /// </summary>
    public int Limit { get; init; } = 50;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiTeamFilesCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Query).NotEmpty().MaximumLength(100).WithMessage("搜索关键字不能为空且长度不超过 100.");
        validate.RuleFor(x => x.Limit).InclusiveBetween(1, 100).WithMessage("返回条数上限必须在 1-100 之间.");
    }
}
