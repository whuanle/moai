using FastEndpoints;
using FluentValidation;
using MoAI.Login.Queries;

namespace MoAI.Login.Validators;

/// <inheritdoc/>
public class QueryRepeatedUserNameCommandValidator : AbstractValidator<QueryRepeatedUserNameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRepeatedUserNameCommandValidator"/> class.
    /// </summary>
    public QueryRepeatedUserNameCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MinimumLength(5).MaximumLength(20).WithMessage("长度 5-30 字符.");
    }
}
