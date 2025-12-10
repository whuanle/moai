using FluentValidation.Results;
using System.Diagnostics;

namespace MoAI.Infra.Models;

/// <summary>
/// 错误响应模型.
/// </summary>
public class BusinessValidationResult
{
    /// <summary>
    /// 请求上下文 ID.
    /// </summary>
    public string RequestId { get; init; } = Activity.Current?.TraceId.ToString() ?? string.Empty;

    /// <summary>
    /// 错误码.
    /// </summary>
    public int Code { get; init; } = 500;

    /// <summary>
    /// 错误消息.
    /// </summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// 具体错误列表.
    /// </summary>
    public IReadOnlyCollection<BusinessExceptionError>? Errors { get; init; } = Array.Empty<BusinessExceptionError>();

    /// <summary>
    /// 扩展.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessValidationResult"/> class.
    /// </summary>
    public BusinessValidationResult()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessValidationResult"/> class.
    /// </summary>
    /// <param name="failures"></param>
    /// <param name="statusCode"></param>
    public BusinessValidationResult(IReadOnlyList<ValidationFailure> failures, int statusCode = 400)
    {
        // Microsoft.AspNetCore.Mvc.ValidationProblemDetails
        Code = statusCode;
        Errors = failures.GroupBy(f => f.PropertyName).Select(e => new BusinessExceptionError
        {
            Name = e.Key,
            Errors = e.Select(m => m.ErrorMessage).ToArray()
        }).ToArray();
    }
}