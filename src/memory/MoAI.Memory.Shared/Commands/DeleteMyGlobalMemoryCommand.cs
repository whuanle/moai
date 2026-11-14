using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 删除本人的一条全局记忆.
/// </summary>
public class DeleteMyGlobalMemoryCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteMyGlobalMemoryCommand>, IUserIdContext
{
    /// <summary>
    /// 记忆 id.
    /// </summary>
    public Guid MemoryId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteMyGlobalMemoryCommand> validate)
    {
        validate.RuleFor(x => x.MemoryId).NotEqual(Guid.Empty).WithMessage("记忆 id 不正确.");
    }
}
