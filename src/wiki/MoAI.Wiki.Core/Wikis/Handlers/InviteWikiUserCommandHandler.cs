using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// <inheritdoc cref="InviteWikiUserCommand"/>
/// </summary>
public class InviteWikiUserCommandHandler : IRequestHandler<InviteWikiUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteWikiUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public InviteWikiUserCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(InviteWikiUserCommand request, CancellationToken cancellationToken)
    {
        HashSet<int> userIds = default!;
        if (request.UserIds.Count > 0)
        {
            userIds = request.UserIds.ToHashSet();
        }
        else
        {
            userIds = new HashSet<int>();
        }

        if (request.UserNames != null && request.UserNames.Count > 0)
        {
            var users = await _databaseContext.Users.Where(x => request.UserNames.Contains(x.UserName)).Select(x => x.Id).ToArrayAsync(cancellationToken);
            foreach (var userId in users)
            {
                userIds.Add(userId);
            }
        }

        var isCreator = await _mediator.Send(new QueryWikiCreatorCommand { WikiId = request.WikiId });
        var wikiUsers = await _databaseContext.WikiUsers.Where(x => x.WikiId == request.WikiId).ToListAsync(cancellationToken);

        if (request.IsInvite)
        {
            // 如果是系统知识库，对应管理员来说，不需要邀请
            if (isCreator.IsSystem)
            {
                var adminUsers = await _mediator.Send(new QueryAdminIdsCommand());

                foreach (var item in adminUsers.AdminIds)
                {
                    userIds.Remove(item);
                }
            }
            else
            {
                userIds.Remove(isCreator.CreatorId);
            }

            // 移除已存在的成员，避免重复邀请
            foreach (var wikiUser in wikiUsers)
            {
                userIds.Remove(wikiUser.UserId);
            }

            List<WikiUserEntity> newWikiUsers = new List<WikiUserEntity>();
            foreach (var item in userIds)
            {
                newWikiUsers.Add(new WikiUserEntity
                {
                    WikiId = request.WikiId,
                    UserId = item
                });
            }

            await _databaseContext.WikiUsers.AddRangeAsync(newWikiUsers);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (isCreator.IsSystem)
            {
                var adminUsers = await _mediator.Send(new QueryAdminIdsCommand());

                foreach (var item in adminUsers.AdminIds)
                {
                    userIds.Remove(item);
                }
            }
            else
            {
                userIds.Remove(isCreator.CreatorId);
            }

            var toRemoveUsers = wikiUsers.Where(x => userIds.Contains(x.UserId)).ToList();

            if (toRemoveUsers.Count != 0)
            {
                _databaseContext.WikiUsers.RemoveRange(toRemoveUsers);
                await _databaseContext.SaveChangesAsync(cancellationToken);
            }
        }

        return EmptyCommandResponse.Default;
    }
}
