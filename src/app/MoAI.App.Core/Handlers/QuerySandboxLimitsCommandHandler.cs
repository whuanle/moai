using MediatR;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Settings.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySandboxLimitsCommand"/>
/// </summary>
public class QuerySandboxLimitsCommandHandler : IRequestHandler<QuerySandboxLimitsCommand, QuerySandboxLimitsCommandResponse>
{
    private readonly ISandboxSettingsService _sandboxSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySandboxLimitsCommandHandler"/> class.
    /// </summary>
    /// <param name="sandboxSettingsService">沙箱上限设置读取服务.</param>
    public QuerySandboxLimitsCommandHandler(ISandboxSettingsService sandboxSettingsService)
    {
        _sandboxSettingsService = sandboxSettingsService;
    }

    /// <inheritdoc/>
    public async Task<QuerySandboxLimitsCommandResponse> Handle(QuerySandboxLimitsCommand request, CancellationToken cancellationToken)
    {
        var limits = await _sandboxSettingsService.GetLimitsAsync(cancellationToken);

        return new QuerySandboxLimitsCommandResponse
        {
            MaxTtlSeconds = limits.MaxTtlSeconds,
            MaxCpu = limits.MaxCpu,
            MaxMemory = limits.MaxMemory
        };
    }
}
