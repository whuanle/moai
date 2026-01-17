using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Classify.Commands;

/// <summary>
/// 删除应用分类.
/// </summary>
public class DeleteAppClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAppClassifyCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAppClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");
    }
}
