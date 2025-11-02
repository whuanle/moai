using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// <inheritdoc cref="AddWebDocumentConfigCommand"/>
/// </summary>
public class UpdateWebDocumentConfigCommandHandler : IRequestHandler<UpdateWebDocumentConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWebDocumentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWebDocumentConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWebDocumentConfigCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WikiWebConfigs.FirstOrDefaultAsync(x => x.Id == request.WebConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("网页爬取配置不存在") { StatusCode = 404 };
        }

        entity.IsCrawlOther = request.IsCrawlOther;
        entity.Address = request.Address.ToString();
        entity.Title = request.Title.Trim();
        entity.LimitAddress = request.LimitAddress?.ToString() ?? string.Empty;
        entity.LimitMaxCount = request.LimitMaxCount;
        entity.IsAutoEmbedding = request.IsAutoEmbedding;
        entity.IsWaitJs = request.IsWaitJs;
        entity.Selector = request.Selector ?? string.Empty;

        _databaseContext.WikiWebConfigs.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
