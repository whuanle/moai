using FluentValidation;
using MediatR;
using MoAI.Wiki.Plugins.Feishu.Models;

namespace MoAI.Wiki.Plugins.Feishu.Queries;

/// <summary>
/// 获取飞书配置.
/// </summary>
public class QueryWikiFeishuConfigCommand : IRequest<QueryWikiFeishuConfigCommandResponse>, IModelValidator<QueryWikiFeishuConfigCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiFeishuConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}