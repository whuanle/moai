using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Classify.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Classify.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppClassifyCommand"/>
/// </summary>
public class UpdateAppClassifyCommandHandler : IRequestHandler<UpdateAppClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAppClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppClassifyCommand request, CancellationToken cancellationToken)
    {
        var classify = await _databaseContext.Classifies.FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.Type == "app", cancellationToken);

        if (classify == null)
        {
            throw new BusinessException("分类不存在") { StatusCode = 409 };
        }

        if (request.Name != classify.Name)
        {
            var existClassify = await _databaseContext.Classifies
                .AnyAsync(x => x.Id != request.ClassifyId && x.Type == "app" && x.Name == request.Name, cancellationToken);

            if (existClassify)
            {
                throw new BusinessException("分类已存在") { StatusCode = 409 };
            }
        }

        classify.Name = request.Name;
        classify.Description = request.Description;

        _databaseContext.Update(classify);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
