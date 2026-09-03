using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Classify.Commands;

/// <summary>
/// 删除分类.
/// </summary>
public class DeleteClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteClassifyCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).GreaterThan(0).WithMessage("分类 id 不合法.");
    }
}
