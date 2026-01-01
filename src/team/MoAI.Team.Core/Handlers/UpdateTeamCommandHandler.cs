using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

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
    /// <param name="databaseContext"></param>
    public UpdateTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .Where(x => x.Id == request.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在") { StatusCode = 404 };
        }

        if (!string.IsNullOrEmpty(request.Name) && team.Name != request.Name)
        {
            // 检查新名称是否与其他团队重复
            var exist = await _databaseContext.Teams.AnyAsync(x => x.Name == request.Name && x.Id != request.TeamId, cancellationToken);
            if (exist)
            {
                throw new BusinessException("已存在同名团队") { StatusCode = 409 };
            }

            team.Name = request.Name;
        }

        if (request.Description != null)
        {
            team.Description = request.Description;
        }

        if (request.Avatar != null)
        {
            team.Avatar = request.Avatar;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
