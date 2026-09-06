using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Gateway.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Gateway.Handlers;

/// <summary>
/// 删除团队网关 API Key（软删除）.
/// </summary>
public class DeleteTeamApiKeyCommandHandler : IRequestHandler<DeleteTeamApiKeyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTeamApiKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteTeamApiKeyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteTeamApiKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.TeamApiKeys
            .FirstOrDefaultAsync(x => x.Id == request.ApiKeyId && x.TeamId == request.TeamId, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("密钥不存在.") { StatusCode = 404 };
        }

        // 审计过滤器会把 Delete 状态改写为软删除.
        _databaseContext.TeamApiKeys.Remove(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
