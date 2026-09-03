using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="CreateWikiDocumentCommand"/>
/// </summary>
public class CreateWikiDocumentCommandHandler : IRequestHandler<CreateWikiDocumentCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public CreateWikiDocumentCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        // 内容协作：全体团队成员（含 Member）可创建文档
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var document = new WikiDocumentEntity
        {
            WikiId = request.WikiId,
            Title = request.Title,
            Content = request.Content ?? string.Empty,
        };

        _databaseContext.WikiDocuments.Add(document);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleLong
        {
            Value = document.Id
        };
    }
}
