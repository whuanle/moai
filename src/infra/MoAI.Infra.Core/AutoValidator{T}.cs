using FluentValidation;
using Maomi;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Infra;

/// <summary>
/// 自动模型验证器.
/// </summary>
/// <typeparam name="T">类型.</typeparam>
public class AutoValidator<T> : AbstractValidator<T>, IValidator<T>
    where T : class, IModelValidator<T>, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoValidator{T}"/> class.
    /// </summary>
    public AutoValidator()
    {
        new T().Validate(this);
    }
}