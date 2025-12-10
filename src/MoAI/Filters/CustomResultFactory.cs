using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MoAI.Infra.Models;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace MoAI.Filters;

/// <summary>
/// 自定义模型验证返回结果.
/// </summary>
public class CustomResultFactory : IFluentValidationAutoValidationResultFactory
{
    /// <inheritdoc/>
    public IActionResult CreateActionResult(ActionExecutingContext context, ValidationProblemDetails? validationProblemDetails)
    {
        List<BusinessExceptionError> errors = new();
        Dictionary<string, object?> extensions = new();
        BusinessValidationResult validationResult = new()
        {
            Code = 400,
            Detail = validationProblemDetails!.Detail!,
            Errors = errors,
            Extensions = validationProblemDetails?.Extensions.AsReadOnly()
        };

        if (validationProblemDetails == null)
        {
            return new BadRequestObjectResult(validationResult);
        }

        foreach (var item in validationProblemDetails.Errors)
        {
            errors.Add(new BusinessExceptionError
            {
                Name = item.Key,
                Errors = item.Value
            });
        }

        return new BadRequestObjectResult(validationResult);
    }
}

//public class LowerCaseValidationInterceptor : IGlobalValidationInterceptor
//{
//    public ValidationResult? AfterValidation(ActionExecutingContext actionExecutingContext, IValidationContext validationContext)
//    {
//        throw new NotImplementedException();
//    }

//    public IValidationContext? BeforeValidation(ActionExecutingContext actionExecutingContext, IValidationContext validationContext)
//    {
//        return validationContext;
//    }
//}