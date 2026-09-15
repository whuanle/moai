using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部应用 token 的知识库授权器实现.
/// </summary>
[InjectOnScoped]
public class ExternalWikiAuthorizer : IExternalWikiAuthorizer
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalWikiAuthorizer"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ExternalWikiAuthorizer(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<WikiEntity> AuthorizeAsync(long wikiId, long teamId, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == wikiId, cancellationToken);
        if (wiki == null || wiki.TeamId != teamId)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        return wiki;
    }
}
