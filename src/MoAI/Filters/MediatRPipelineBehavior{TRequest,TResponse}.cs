using Maomi;
using MediatR;
using MoAI.Infra;
using System.Diagnostics;

namespace MoAI.Filters;

/// <summary>
/// MediatR 管道行为.
/// </summary>
/// <typeparam name="TRequest">Command.</typeparam>
/// <typeparam name="TResponse">Response.</typeparam>
public class MediatRPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
{
    private readonly ILogger<MediatRPipelineBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediatRPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public MediatRPipelineBehavior(ILogger<MediatRPipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using var activity = AppConst.ActivitySource.StartActivity($"{requestName}");

        activity?.SetTag("mediatr.request_type", typeof(TRequest).Name);
        activity?.SetTag("mediatr.response_type", typeof(TResponse).Name);

        LogHandling(_logger, requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            LogHandled(_logger, requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            LogFailure(_logger, ex, requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static readonly Action<ILogger, string, Exception?> _logHandling = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(0, nameof(Handle)),
        "Handling MediatR request {RequestName}");

    private static readonly Action<ILogger, string, double, Exception?> _logHandled = LoggerMessage.Define<string, double>(
        LogLevel.Information,
        new EventId(1, nameof(Handle)),
        "Handled MediatR request {RequestName} successfully in {Duration}ms");

    private static readonly Action<ILogger, string, double, Exception?> _logFailure = LoggerMessage.Define<string, double>(
        LogLevel.Error,
        new EventId(2, nameof(Handle)),
        "Failed to handle MediatR request {RequestName} after {Duration}ms");

    private static void LogHandling(ILogger logger, string requestName) =>
        _logHandling(logger, requestName, null);

    private static void LogHandled(ILogger logger, string requestName, double duration) =>
        _logHandled(logger, requestName, duration, null);

    private static void LogFailure(ILogger logger, Exception ex, string requestName, double duration) =>
        _logFailure(logger, requestName, duration, ex);
}