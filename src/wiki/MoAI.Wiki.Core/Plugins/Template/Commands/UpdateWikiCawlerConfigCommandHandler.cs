using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// <inheritdoc cref="UpdateWikiPluginConfigCommand{T}"/>
/// </summary>
public class UpdateWikiPluginConfigCommandHandler<T> : IRequestHandler<UpdateWikiPluginConfigCommand<T>, EmptyCommandResponse>
        where T : class, IWikiPluginKey
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiPluginConfigCommandHandler{T}"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiPluginConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiPluginConfigCommand<T> request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("配置不存在") { StatusCode = 404 };
        }

        entity.Title = request.Title.Trim();
        entity.Config = request.Config.ToJsonString();

        _databaseContext.WikiPluginConfigs.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
