using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Models;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 把本人的一条应用内记忆存为全局记忆（跨应用可用；原应用记忆保留）.
/// </summary>
public class PromoteMemoryToGlobalCommand : IRequest<MemoryItem>, IModelValidator<PromoteMemoryToGlobalCommand>, IUserIdContext
{
    /// <summary>
    /// 来源应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 来源记忆 id.
    /// </summary>
    public Guid MemoryId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PromoteMemoryToGlobalCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEqual(Guid.Empty).WithMessage("应用 id 不正确.");
        validate.RuleFor(x => x.MemoryId).NotEqual(Guid.Empty).WithMessage("记忆 id 不正确.");
    }
}
