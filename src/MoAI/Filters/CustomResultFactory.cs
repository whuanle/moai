using FluentValidation;
using FluentValidation.Results;
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
    public Task<IActionResult?> CreateActionResult(ActionExecutingContext context, ValidationProblemDetails validationProblemDetails, IDictionary<IValidationContext, ValidationResult> validationResults)
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
            return Task.FromResult<IActionResult?>(new BadRequestObjectResult(validationResult));
        }

        foreach (var item in validationProblemDetails.Errors)
        {
            errors.Add(new BusinessExceptionError
            {
                Name = item.Key,
                Errors = item.Value
            });
        }

        return Task.FromResult<IActionResult?>(new BadRequestObjectResult(validationResult));
    }
}