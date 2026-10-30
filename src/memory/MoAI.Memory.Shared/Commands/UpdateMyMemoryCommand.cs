using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 编辑本人的一条应用内长期记忆（保留原 id 与创建时间，内容重嵌向量）.
/// </summary>
public class UpdateMyMemoryCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateMyMemoryCommand>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 记忆 id.
    /// </summary>
    public Guid MemoryId { get; init; }

    /// <summary>
    /// 新记忆内容.
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
    public static void Validate(AbstractValidator<UpdateMyMemoryCommand> validate)
    {
        validate.RuleFor(x => x.MemoryId).NotEqual(Guid.Empty).WithMessage("记忆 id 不正确.");
        validate.RuleFor(x => x.Content).NotEmpty().MaximumLength(500).WithMessage("记忆内容不能为空且不超过 500 字.");
        validate.RuleFor(x => x.MemoryType).InclusiveBetween(0, 2).WithMessage("记忆类型不正确.");
    }
}
