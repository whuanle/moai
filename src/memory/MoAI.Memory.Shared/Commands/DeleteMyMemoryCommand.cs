using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 删除本人的一条长期记忆.
/// </summary>
public class DeleteMyMemoryCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteMyMemoryCommand>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

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
    public static void Validate(AbstractValidator<DeleteMyMemoryCommand> validate)
    {
        validate.RuleFor(x => x.MemoryId).NotEqual(Guid.Empty).WithMessage("记忆 id 不正确.");
    }
}
