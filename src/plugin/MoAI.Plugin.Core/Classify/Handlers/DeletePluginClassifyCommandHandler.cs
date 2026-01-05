using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Classify.Commands;

namespace MoAI.Plugin.ClassifyHandlers.Handlers;

/// <summary>
/// <inheritdoc cref="DeletePluginClassifyCommand"/>
/// </summary>
public class DeletePluginClassifyCommandHandler : IRequestHandler<DeletePluginClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePluginClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeletePluginClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePluginClassifyCommand request, CancellationToken cancellationToken)
    {
        var classifyEntity = await _databaseContext.Classifies.FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.Type == "plugin", cancellationToken);
        if (classifyEntity == null)
        {
            throw new BusinessException("分类不存在");
        }

        _databaseContext.Classifies.Remove(classifyEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
