using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

/// <summary>
/// 检查用户名是否重复.
/// </summary>
public class QueryRepeatedUserNameCommand : IRequest<SimpleBool>, IModelValidator<QueryRepeatedUserNameCommand>
{
    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryRepeatedUserNameCommand> validate)
    {
        validate.RuleFor(x => x.UserName).NotEmpty().MinimumLength(5).MaximumLength(20).WithMessage("长度 5-30 字符.");
    }
}