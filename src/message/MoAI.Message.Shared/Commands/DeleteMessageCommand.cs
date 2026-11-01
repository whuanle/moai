using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Message.Commands;

/// <summary>
/// 删除站内信（软删除）.
/// </summary>
public class DeleteMessageCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteMessageCommand>, IUserIdContext
{
    /// <summary>
    /// 站内信 id 集合.
    /// </summary>
    public IReadOnlyList<long> MessageIds { get; init; } = new List<long>();

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteMessageCommand> validate)
    {
        validate.RuleFor(x => x.MessageIds)
            .NotEmpty().WithMessage("站内信 id 不能为空.")
            .Must(x => x.Count <= 200).WithMessage("单次最多删除 200 条.")
            .Must(x => x.All(id => id > 0)).WithMessage("站内信 id 不合法.");
    }
}
