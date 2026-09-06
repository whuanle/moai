using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Gateway.Commands;

/// <summary>
/// 团队管理员删除网关 API Key（软删除）.
/// </summary>
public class DeleteTeamApiKeyCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<DeleteTeamApiKeyCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 密钥 id.
    /// </summary>
    public Guid ApiKeyId { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteTeamApiKeyCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).NotEmpty().WithMessage("团队id不能为空.");
        validate.RuleFor(x => x.ApiKeyId).NotEmpty().WithMessage("密钥id不能为空.");
    }
}
