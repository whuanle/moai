using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.BuiltCommands;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="DeleteInternalPluginCommand"/>
/// </summary>
public class DeleteInternalPluginCommandHandler : IRequestHandler<DeleteInternalPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteInternalPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteInternalPluginCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<EmptyCommandResponse> Handle(DeleteInternalPluginCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.PluginInternals.FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("未找到插件实例") { StatusCode = 404 };
        }

        _databaseContext.PluginInternals.Remove(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
