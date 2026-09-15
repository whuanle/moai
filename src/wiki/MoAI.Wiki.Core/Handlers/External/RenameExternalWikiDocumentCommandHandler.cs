using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="RenameExternalWikiDocumentCommand"/>
/// </summary>
public class RenameExternalWikiDocumentCommandHandler : IRequestHandler<RenameExternalWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameExternalWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public RenameExternalWikiDocumentCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RenameExternalWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.Id == request.DocumentId, cancellationToken);

        if (document == null)
        {
            throw new BusinessException("文档不存在.") { StatusCode = 404 };
        }

        document.FileName = request.FileName;
        document.FileType = Path.GetExtension(request.FileName);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
