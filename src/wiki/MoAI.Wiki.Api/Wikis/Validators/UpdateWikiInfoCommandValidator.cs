//using FastEndpoints;
//using FluentValidation;
//using MoAI.Wiki.Wikis.Commands;

//namespace MoAI.Wiki.Wikis.Validators;

///// <summary>
///// UpdateWikiInfoCommandValidator
///// </summary>
//public class UpdateWikiInfoCommandValidator : AbstractValidator<UpdateWikiInfoCommand>
//{
//    /// <summary>
//    /// Initializes a new instance of the <see cref="UpdateWikiInfoCommandValidator"/> class.
//    /// </summary>
//    public UpdateWikiInfoCommandValidator()
//    {
//        RuleFor(x => x.WikiId)
//            .NotEmpty().WithMessage("知识库id不正确")
//            .GreaterThan(0).WithMessage("知识库id不正确");

//        RuleFor(x => x.Name)
//            .NotEmpty().WithMessage("知识库名称长度在 2-20 之间.")
//            .Length(2, 20).WithMessage("知识库名称长度在 2-20 之间.");

//        RuleFor(x => x.Description)
//            .NotEmpty().WithMessage("知识库描述长度在 2-255 之间.")
//            .Length(2, 255).WithMessage("知识库描述长度在 2-255 之间.");
//    }
//}
