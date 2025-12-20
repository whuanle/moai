using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.Plugin.NativePlugins.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryNativePluginDetailCommand"/>
/// </summary>
public class QueryNativePluginDetailCommandHandler : IRequestHandler<QueryNativePluginDetailCommand, NativePluginDetail>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNativePluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="nativePluginFactory"></param>
    public QueryNativePluginDetailCommandHandler(DatabaseContext databaseContext, IMediator mediator, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<NativePluginDetail> Handle(QueryNativePluginDetailCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins.Where(x => x.Id == request.PluginId)
            .Join(_databaseContext.PluginNatives, a => a.PluginId, a => a.Id, (x, y) => new NativePluginDetail
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
                Config = y.Config,
                TemplatePluginClassify = y.TemplatePluginClassify.JsonToObject<NativePluginClassify>(),
                TemplatePluginKey = y.TemplatePluginKey,
            }).FirstOrDefaultAsync();

        if (plugin == null)
        {
            throw new BusinessException("未找到插件") { StatusCode = 404 };
        }

        await _mediator.Send(new FillUserInfoCommand { Items = new NativePluginDetail[] { plugin } });

        return plugin;
    }
}
