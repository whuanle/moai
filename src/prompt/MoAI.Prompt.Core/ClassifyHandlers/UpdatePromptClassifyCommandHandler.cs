using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Prompt.Commands;

namespace MoAI.Plugin.ClassifyHandlers;

/// <summary>
/// <inheritdoc cref="UpdatePromptClassifyCommand"/>
/// </summary>
public class UpdatePromptClassifyCommandHandler : IRequestHandler<UpdatePromptClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdatePromptClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePromptClassifyCommand request, CancellationToken cancellationToken)
    {
        var classify = await _databaseContext.Classifies.FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.Type == "prompt", cancellationToken);

        if (classify == null)
        {
            throw new BusinessException("分类不存在") { StatusCode = 409 };
        }

        if (request.Name != classify.Name)
        {
            var existClassify = await _databaseContext.Classifies
                .AnyAsync(x => x.Id != request.ClassifyId && x.Type == "prompt" && x.Name == request.Name, cancellationToken);

            if (existClassify == true)
            {
                throw new BusinessException("分类已存在") { StatusCode = 409 };
            }
        }

        classify.Name = request.Name;
        classify.Description = request.Description ?? string.Empty;

        _databaseContext.Update(classify);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
