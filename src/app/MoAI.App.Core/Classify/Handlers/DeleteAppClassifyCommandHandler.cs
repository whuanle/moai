using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Classify.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Classify.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAppClassifyCommand"/>
/// </summary>
public class DeleteAppClassifyCommandHandler : IRequestHandler<DeleteAppClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAppClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAppClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAppClassifyCommand request, CancellationToken cancellationToken)
    {
        var classifyEntity = await _databaseContext.Classifies.FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.Type == "app", cancellationToken);
        if (classifyEntity == null)
        {
            throw new BusinessException("分类不存在");
        }

        _databaseContext.Classifies.Remove(classifyEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
