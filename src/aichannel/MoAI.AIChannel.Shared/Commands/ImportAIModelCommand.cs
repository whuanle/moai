using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.AIChannel.Models;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 批量导入模型列表（前端从 opencode models.json 解析后提交）.
/// </summary>
public class ImportAIModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<ImportAIModelCommand>
{
    /// <summary>
    /// 所属渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// 待导入的模型列表.
    /// </summary>
    public List<AIChannelModelMeta> Items { get; init; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.");
        validate.RuleFor(x => x.Items).NotNull().WithMessage("模型列表不能为空.");
    }
}
