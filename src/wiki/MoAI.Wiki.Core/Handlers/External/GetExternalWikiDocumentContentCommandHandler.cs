using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="GetExternalWikiDocumentContentCommand"/>
/// </summary>
public class GetExternalWikiDocumentContentCommandHandler : IRequestHandler<GetExternalWikiDocumentContentCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetExternalWikiDocumentContentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public GetExternalWikiDocumentContentCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(GetExternalWikiDocumentContentCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var content = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == request.DocumentId && x.WikiId == request.WikiId && x.IsDeleted == 0, cancellationToken)
            ?? throw new BusinessException("文档内容尚未提取.") { StatusCode = 404 };

        if (string.IsNullOrWhiteSpace(content.Content))
        {
            throw new BusinessException("文档内容为空.") { StatusCode = 404 };
        }

        return new SimpleString
        {
            Value = content.Content
        };
    }
}
