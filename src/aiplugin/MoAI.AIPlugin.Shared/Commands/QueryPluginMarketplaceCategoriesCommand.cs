using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>查询 MoPlatform 在线插件分类.</summary>
public sealed class QueryPluginMarketplaceCategoriesCommand : IRequest<IReadOnlyList<PluginMarketplaceCategory>>, IModelValidator<QueryPluginMarketplaceCategoriesCommand>
{
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginMarketplaceCategoriesCommand> validate)
    {
    }
}
