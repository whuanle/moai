using System;
using FluentValidation;
using MediatR;
using MoAI.AIChannel.Models;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 创建 AI 模型（手动添加，元数据可来自 models.json）.
/// </summary>
public class CreateAIModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<CreateAIModelCommand>
{
    /// <summary>
    /// 所属渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// 模型元数据.
    /// </summary>
    public AIChannelModelMeta Meta { get; init; } = default!;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.");
        validate.RuleFor(x => x.Meta).NotNull().WithMessage("模型元数据不能为空.");
        validate.RuleFor(x => x.Meta.ModelId).NotEmpty().WithMessage("模型标识不能为空.").MaximumLength(200).WithMessage("模型标识最长 200 个字符.");
        validate.RuleFor(x => x.Meta.Name).NotEmpty().WithMessage("模型名称不能为空.").MaximumLength(200).WithMessage("模型名称最长 200 个字符.");
    }
}
