#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MediatR;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryInternalTemplatePluginListCommand"/>
/// </summary>
public class QueryInternalTemplatePluginListCommandHandler : IRequestHandler<QueryInternalTemplatePluginListCommand, QueryInternalTemplatePluginListCommandResponse>
{
    /// <inheritdoc/>
    public async Task<QueryInternalTemplatePluginListCommandResponse> Handle(QueryInternalTemplatePluginListCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        return new QueryInternalTemplatePluginListCommandResponse
        {
            Plugins = InternalPluginFactory.Plugins.Select(x => new InternalTemplatePlugin
            {
                Description = x.Value.Description,
                TemplatePluginKey = x.Value.PluginKey,
                PluginName = x.Value.PluginName,
                Classify = x.Value.Classify
            }).ToArray()
        };
    }
}
