using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.NativePlugins.Commands;

namespace MoAI.Plugin.Commands;

/// <summary>
/// <inheritdoc cref="DeleteNativePluginCommand"/>
/// </summary>
public class DeleteNativePluginCommandHandler : IRequestHandler<DeleteNativePluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteNativePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteNativePluginCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteNativePluginCommand request, CancellationToken cancellationToken)
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
