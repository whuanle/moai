//using FastEndpoints;
//using FluentValidation;
//using MoAI.Wiki.Wikis.Commands;

//namespace MoAI.Wiki.Wikis.Validators;

///// <summary>
///// RemoveWikiUserCommandValidator
///// </summary>
//public class RemoveWikiUserCommandValidator : Validator<RemoveWikiUserCommand>
//{
//    /// <summary>
//    /// Initializes a new instance of the <see cref="RemoveWikiUserCommandValidator"/> class.
//    /// </summary>
//    public RemoveWikiUserCommandValidator()
//    {
//        RuleFor(x => x.WikiId)
//            .NotEmpty().WithMessage("知识库id不正确")
//            .GreaterThan(0).WithMessage("知识库id不正确");

//        RuleFor(x => x.UserIds)
//            .NotNull().WithMessage("未指定要移除的用户")
//            .Must(x => x.Count > 0).WithMessage("未指定要移除的用户");
//    }
//}
