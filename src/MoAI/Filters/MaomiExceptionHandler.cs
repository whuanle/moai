using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Filters;

/// <summary>
/// 异常拦截.<br />
/// </summary>
public class MaomiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<MaomiExceptionHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaomiExceptionHandler"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public MaomiExceptionHandler(ILogger<MaomiExceptionHandler> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BusinessException businessException)
        {
            HandleBusinessException(httpContext, businessException, cancellationToken);
        }
        else
        {
            ProcessUnhandledException(httpContext, exception, cancellationToken);
        }

        return ValueTask.FromResult(true);
    }

    private static void ProcessUnhandledException(HttpContext context, Exception ex, CancellationToken cancellationToken)
    {
        var message = string.Empty;
        var messageDetail = string.Empty;

#if DEBUG
        message = ex.Message;
#else
        message = "Internal server error";
#endif

        var response = new BusinessValidationResult()
        {
            Code = 500,
            RequestId = context.TraceIdentifier,
            Detail = message,
        };

        context.Response.StatusCode = 500;

        context.Response.WriteAsJsonAsync(
            response,
            cancellationToken: cancellationToken);
    }

    private static void HandleBusinessException(HttpContext httpContext, BusinessException businessException, CancellationToken cancellationToken)
    {
        // 本类是单例，IStringLocalizer 依赖请求级 I18nContext，从请求作用域解析；
        // 消息原文即 i18n key，资源缺失时 IStringLocalizer 原样返回 key，行为与未翻译一致
        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer>();
        var message = businessException.Argments?.Count > 0
            ? localizer[businessException.Message, businessException.Argments.ToArray()].Value
            : localizer[businessException.Message].Value;

#if DEBUG
        var messageDetail = businessException.ToString();
#endif

        var response = new BusinessValidationResult()
        {
            Code = businessException.StatusCode,
            RequestId = httpContext.TraceIdentifier,
            Detail = message,
        };

        httpContext.Response.StatusCode = businessException.StatusCode;

        httpContext.Response.WriteAsJsonAsync(
            response,
            cancellationToken: cancellationToken);
    }
}
