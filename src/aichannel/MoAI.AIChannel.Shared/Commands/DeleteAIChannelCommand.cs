using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 删除 AI 渠道.
/// </summary>
public class DeleteAIChannelCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAIChannelCommand>
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAIChannelCommand> validate)
    {
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.");
    }
}
