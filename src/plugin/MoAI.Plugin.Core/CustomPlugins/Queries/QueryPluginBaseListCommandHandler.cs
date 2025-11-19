using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.Models;
using MoAI.User.Queries;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginBaseListCommand"/>
/// </summary>
public class QueryPluginBaseListCommandHandler : IRequestHandler<QueryPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryPluginBaseListCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginBaseListCommandResponse> Handle(QueryPluginBaseListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins.AsQueryable();

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.PluginName.Contains(request.Name));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(x => x.Type == (int)request.Type.Value);
        }

        if (request.ClassifyId.HasValue)
        {
            query = query.Where(x => x.ClassifyId == request.ClassifyId.Value);
        }

        if (request.IsPublic.HasValue)
        {
            query = query.Where(x => x.IsPublic == request.IsPublic.Value);
        }

        var plugins = await query
            .Select(x => new PluginBaseInfoItem
            {
                PluginId = x.Id,
                Server = x.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = x.OpenapiFileId,
                OpenapiFileName = x.OpenapiFileName,
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId
            }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand { Items = plugins });

        return new QueryPluginBaseListCommandResponse { Items = plugins };
    }
}
