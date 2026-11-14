using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 清空本人的全部全局记忆（危险操作，前端需二次确认）.
/// </summary>
public class ClearMyGlobalMemoryCommand : IRequest<EmptyCommandResponse>, IModelValidator<ClearMyGlobalMemoryCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ClearMyGlobalMemoryCommand> validate)
    {
    }
}
