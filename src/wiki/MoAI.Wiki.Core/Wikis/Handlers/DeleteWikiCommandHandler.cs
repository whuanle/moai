using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 删除 wiki.
/// </summary>
public class DeleteWikiCommandHandler : IRequestHandler<DeleteWikiCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteWikiCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).FirstOrDefaultAsync();
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        _databaseContext.Remove(wiki);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // todo: 发出命令清理内容
        return EmptyCommandResponse.Default;
    }
}