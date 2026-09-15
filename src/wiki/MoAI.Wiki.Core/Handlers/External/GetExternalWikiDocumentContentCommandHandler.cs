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

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId && x.WikiId == request.WikiId, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("文档不存在.") { StatusCode = 404 };
        }

        var content = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == request.DocumentId && x.WikiId == request.WikiId, cancellationToken);

        // 文档存在但尚未提取（或提取结果为空）与"文档不存在"保持相同 404 状态码，仅以消息区分.
        if (content == null || string.IsNullOrWhiteSpace(content.Content))
        {
            throw new BusinessException("文档内容尚未提取.") { StatusCode = 404 };
        }

        return new SimpleString
        {
            Value = content.Content
        };
    }
}
