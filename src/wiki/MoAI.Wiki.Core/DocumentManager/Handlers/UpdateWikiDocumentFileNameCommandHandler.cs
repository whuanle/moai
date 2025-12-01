using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using System.Transactions;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 修改知识库文档名称命令处理器
/// </summary>
public class UpdateWikiDocumentFileNameCommandHandler : IRequestHandler<UpdateWikiDocumentFileNameCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiDocumentFileNameCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiDocumentFileNameCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc />
    public async Task<EmptyCommandResponse> Handle(UpdateWikiDocumentFileNameCommand request, CancellationToken cancellationToken)
    {
        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.Id == request.DocumentId, cancellationToken);

        if (document is null)
        {
            throw new BusinessException("文档不存在");
        }

        document.FileName = request.FileName;
        document.FileType = Path.GetExtension(request.FileName);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}