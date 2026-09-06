using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Gateway.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Gateway.Handlers;

/// <summary>
/// 修改团队网关 API Key.
/// </summary>
public class UpdateTeamApiKeyCommandHandler : IRequestHandler<UpdateTeamApiKeyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamApiKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateTeamApiKeyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamApiKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.TeamApiKeys
            .FirstOrDefaultAsync(x => x.Id == request.ApiKeyId && x.TeamId == request.TeamId, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("密钥不存在.") { StatusCode = 404 };
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            entity.Name = request.Name;
        }

        entity.IsDisable = request.IsDisable;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
