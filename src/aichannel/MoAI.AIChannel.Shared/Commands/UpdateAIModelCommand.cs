using System;
using FluentValidation;
using MediatR;
using MoAI.AIChannel.Models;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 更新 AI 模型.
/// </summary>
public class UpdateAIModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateAIModelCommand>
{
    /// <summary>
    /// 模型 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 模型元数据.
    /// </summary>
    public AIChannelModelMeta Meta { get; init; } = default!;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAIModelCommand> validate)
    {
        validate.RuleFor(x => x.Meta).NotNull().WithMessage("模型元数据不能为空.");
        validate.RuleFor(x => x.Meta.ModelId).NotEmpty().WithMessage("模型标识不能为空.").MaximumLength(200).WithMessage("模型标识最长 200 个字符.");
        validate.RuleFor(x => x.Meta.Name).NotEmpty().WithMessage("模型名称不能为空.").MaximumLength(200).WithMessage("模型名称最长 200 个字符.");
    }
}
