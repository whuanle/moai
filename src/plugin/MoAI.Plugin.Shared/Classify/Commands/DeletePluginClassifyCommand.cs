using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Classify.Commands;

/// <summary>
/// 删除插件分类.
/// </summary>
public class DeletePluginClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeletePluginClassifyCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeletePluginClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");
    }
}