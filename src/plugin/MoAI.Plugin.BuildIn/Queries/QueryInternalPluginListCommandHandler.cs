using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.InternalPluginQueries;
using MoAI.Plugin.InternalPluginQueries.Responses;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryInternalPluginListCommand"/>
/// </summary>
public class QueryInternalPluginListCommandHandler : IRequestHandler<QueryInternalPluginListCommand, QueryInternalPluginListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryInternalPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryInternalPluginListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryInternalPluginListCommandResponse> Handle(QueryInternalPluginListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.PluginInternals.AsQueryable();

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.PluginName.Contains(request.Name));
        }

        if (request.ClassifyId.HasValue)
        {
            query = query.Where(x => x.ClassifyId == (int)request.ClassifyId.Value);
        }

        if (request.IsPublic.HasValue)
        {
            query = query.Where(x => x.IsPublic == request.IsPublic.Value);
        }

        var plugins = await query
            .Select(x => new InternalPluginInfo
            {
                PluginId = x.Id,
                PluginName = x.PluginName,
                Title = x.Title,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId
            }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand { Items = plugins });

        return new QueryInternalPluginListCommandResponse { Items = plugins };
    }
}
