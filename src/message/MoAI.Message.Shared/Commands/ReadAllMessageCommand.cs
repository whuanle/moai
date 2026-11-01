using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Message.Commands;

/// <summary>
/// 全部标记已读.
/// </summary>
public class ReadAllMessageCommand : IRequest<EmptyCommandResponse>, IModelValidator<ReadAllMessageCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ReadAllMessageCommand> validate)
    {
    }
}
