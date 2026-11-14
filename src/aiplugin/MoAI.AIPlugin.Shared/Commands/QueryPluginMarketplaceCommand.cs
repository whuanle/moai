using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>搜索 MoPlatform 在线插件目录.</summary>
public sealed class QueryPluginMarketplaceCommand : IRequest<PluginMarketplacePage>, IModelValidator<QueryPluginMarketplaceCommand>
{
    /// <summary>关键字.</summary>
    public string? Keyword { get; init; }

    /// <summary>MoPlatform 分类 id.</summary>
    public string? CategoryId { get; init; }

    /// <summary>页码.</summary>
    public int PageNo { get; init; } = 1;

    /// <summary>每页条数.</summary>
    public int PageSize { get; init; } = 20;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginMarketplaceCommand> validate)
    {
        validate.RuleFor(x => x.Keyword).MaximumLength(256);
        validate.RuleFor(x => x.CategoryId).MaximumLength(20);
        validate.RuleFor(x => x.CategoryId).Matches("^[0-9]+$").When(x => !string.IsNullOrWhiteSpace(x.CategoryId));
        validate.RuleFor(x => x.PageNo).InclusiveBetween(1, 1_000_000);
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
