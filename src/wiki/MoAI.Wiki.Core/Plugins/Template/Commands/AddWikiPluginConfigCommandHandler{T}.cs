using Maomi;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// <inheritdoc cref="AddWikiPluginConfigCommand{T}"/>
/// </summary>
/// <typeparam name="T">类型.</typeparam>
public class AddWikiPluginConfigCommandHandler<T> : IRequestHandler<AddWikiPluginConfigCommand<T>, SimpleInt>
        where T : class, IWikiPluginKey
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiPluginConfigCommandHandler{T}"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AddWikiPluginConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(AddWikiPluginConfigCommand<T> request, CancellationToken cancellationToken)
    {
        var existWiki = await _databaseContext.Wikis.AnyAsync(x => x.Id == request.WikiId, cancellationToken);
        if (!existWiki)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        var existConfig = await _databaseContext.WikiPluginConfigs.AnyAsync(x => x.WikiId == request.WikiId && x.Title == request.Title && x.PluginType == request.Config.PluginKey, cancellationToken);
        if (existConfig)
        {
            throw new BusinessException("存在相同名称的配置");
        }

        var entity = new WikiPluginConfigEntity
        {
            WikiId = request.WikiId,
            Title = request.Title,
            PluginType = request.Config.PluginKey,
            Config = request.Config.ToJsonString()
        };

        _databaseContext.WikiPluginConfigs.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleInt { Value = entity.Id };
    }
}
