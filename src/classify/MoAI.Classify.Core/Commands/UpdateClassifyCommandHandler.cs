using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Classify.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Classify.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateClassifyCommand"/>
/// </summary>
public class UpdateClassifyCommandHandler : IRequestHandler<UpdateClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateClassifyCommand request, CancellationToken cancellationToken)
    {
        var classify = await _databaseContext.Classifies
            .FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.IsDeleted == 0, cancellationToken);

        if (classify == null)
        {
            throw new BusinessException("未找到分类，请检查 id 是否正确.") { StatusCode = 404 };
        }

        if (classify.Name != request.Name)
        {
            var exist = await _databaseContext.Classifies
                .AnyAsync(x => x.Id != request.ClassifyId && x.Type == classify.Type && x.Name == request.Name && x.IsDeleted == 0, cancellationToken);

            if (exist)
            {
                throw new BusinessException("分类名称已存在，请更换后重试.") { StatusCode = 409 };
            }
        }

        classify.Name = request.Name;
        classify.Description = request.Description ?? classify.Description;

        _databaseContext.Update(classify);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
