using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamCommand"/>
/// </summary>
public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && team.Name != request.Name)
        {
            var nameExist = await _databaseContext.Teams
                .AnyAsync(x => x.Name == request.Name, cancellationToken);

            if (nameExist)
            {
                throw new BusinessException("团队名称已存在，请更换后重试.") { StatusCode = 409 };
            }

            team.Name = request.Name;
        }

        if (request.Description != null)
        {
            team.Description = request.Description;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
