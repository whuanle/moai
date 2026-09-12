using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 删除会话（软删除，连同消息）；仅会话归属用户可操作.
/// </summary>
public class DeleteAppSessionCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<DeleteAppSessionCommand>
{
    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAppSessionCommand> validate)
    {
        validate.RuleFor(x => x.SessionId).NotEmpty().WithMessage("会话 id 不正确.");
    }
}
