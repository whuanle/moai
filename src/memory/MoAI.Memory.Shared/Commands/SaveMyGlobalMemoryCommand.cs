using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Models;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 新增本人的一条全局记忆（个人跨应用记忆）.
/// </summary>
public class SaveMyGlobalMemoryCommand : IRequest<MemoryItem>, IModelValidator<SaveMyGlobalMemoryCommand>, IUserIdContext
{
    /// <summary>
    /// 记忆内容.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 记忆类型（0 语义 / 1 情景 / 2 程序）.
    /// </summary>
    public int MemoryType { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveMyGlobalMemoryCommand> validate)
    {
        validate.RuleFor(x => x.Content).NotEmpty().MaximumLength(500).WithMessage("记忆内容不能为空且不超过 500 字.");
        validate.RuleFor(x => x.MemoryType).InclusiveBetween(0, 2).WithMessage("记忆类型不正确.");
    }
}
