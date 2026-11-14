using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Message.Commands;

/// <summary>
/// 标记站内信已读.
/// </summary>
public class ReadMessageCommand : IRequest<EmptyCommandResponse>, IModelValidator<ReadMessageCommand>, IUserIdContext
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
    public static void Validate(AbstractValidator<ReadMessageCommand> validate)
    {
        validate.RuleFor(x => x.MessageIds)
            .NotEmpty().WithMessage("站内信 id 不能为空.")
            .Must(x => x.Count <= 200).WithMessage("单次最多标记 200 条.")
            .Must(x => x.All(id => id > 0)).WithMessage("站内信 id 不合法.");
    }
}
