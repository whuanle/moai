using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.InternalPluginQueries;
using MoAI.Plugin.InternalPluginQueries.Responses;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryInternalPluginDetailCommand"/>
/// </summary>
public class QueryInternalPluginDetailCommandHandler : IRequestHandler<QueryInternalPluginDetailCommand, InternalPluginDetail>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryInternalPluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryInternalPluginDetailCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<InternalPluginDetail> Handle(QueryInternalPluginDetailCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.PluginInternals.Where(x => x.Id == request.PluginId)
            .Select(x => new InternalPluginDetail
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
                ClassifyId = x.ClassifyId,
                Params = x.Config,
                TemplatePluginClassify = x.TemplatePluginClassify.JsonToObject<InternalPluginClassify>(),
                TemplatePluginKey = x.TemplatePluginKey,
            }).FirstOrDefaultAsync();

        if (plugin == null)
        {
            throw new BusinessException("未找到插件") { StatusCode = 404 };
        }

        await _mediator.Send(new FillUserInfoCommand { Items = new InternalPluginDetail[] { plugin } });

        return plugin;
    }
}
