using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 从供应商拉取并同步模型列表（后端负责获取模型并匹配内置 models.json）.
/// </summary>
public class SyncAIModelCommand : IRequest<SyncAIModelCommandResponse>, IModelValidator<SyncAIModelCommand>
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SyncAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.");
    }
}
