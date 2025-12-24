using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.Plugin.NativePlugins.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryNativePluginListCommand"/>
/// </summary>
public class QueryNativePluginListCommandHandler : IRequestHandler<QueryNativePluginListCommand, QueryNativePluginListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly INativePluginFactory _nativePluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNativePluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="nativePluginFactory"></param>
    public QueryNativePluginListCommandHandler(DatabaseContext databaseContext, IMediator mediator, INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _nativePluginFactory = nativePluginFactory;
    }

    /// <inheritdoc/>
    public async Task<QueryNativePluginListCommandResponse> Handle(QueryNativePluginListCommand request, CancellationToken cancellationToken)
    {
        var pluginTemplates = _nativePluginFactory.GetPlugins();
        var pluginTemplatesQuery = pluginTemplates.AsQueryable().Where(x => x.PluginType == PluginType.ToolPlugin);

        var query = _databaseContext.Plugins.Where(x => x.Type == (int)PluginType.NativePlugin || x.Type == (int)PluginType.ToolPlugin);

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            query = query.Where(x => x.PluginName.Contains(request.Keyword) || x.Title.Contains(request.Keyword));
            pluginTemplatesQuery = pluginTemplatesQuery.Where(x => x.Key.Contains(request.Keyword) || x.Name.Contains(request.Keyword));
        }

        if (request.ClassifyId != null && request.ClassifyId.Value > 0)
        {
            query = query.Where(x => x.ClassifyId == request.ClassifyId.Value);
        }

        if (request.IsPublic != null)
        {
            query = query.Where(x => x.IsPublic == request.IsPublic.Value);
        }

        var nativePluginsQuery = _databaseContext.PluginNatives.AsQueryable();
        if (request.TemplatePluginClassify != null)
        {
            nativePluginsQuery = nativePluginsQuery.Where(x => x.TemplatePluginClassify == request.TemplatePluginClassify.ToJsonString());
            pluginTemplatesQuery = pluginTemplatesQuery.Where(x => x.Classify == request.TemplatePluginClassify.Value);
        }

        var nativePlugins = await query.Join(nativePluginsQuery, a => a.PluginId, b => b.Id, (x, y) =>
                new NativePluginInfo
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
                    TemplatePluginClassify = y.TemplatePluginClassify.JsonToObject<NativePluginClassify>(),
                    TemplatePluginKey = y.TemplatePluginKey
                }).ToListAsync();

        if (request.ClassifyId == null || request.ClassifyId == 0)
        {
            var filtterPluginTemplates = pluginTemplatesQuery.ToArray();

            // 从 pluginTemplates 合上去
            foreach (var item in filtterPluginTemplates)
            {
                var toolPlugin = nativePlugins.FirstOrDefault(x => x.TemplatePluginKey == item.Key);
                if (toolPlugin == null)
                {
                    nativePlugins.Add(new NativePluginInfo
                    {
                        PluginId = 0,
                        Description = item.Description,
                        Title = item.Name,
                        TemplatePluginClassify = item.Classify,
                        TemplatePluginKey = item.Key,
                        CreateTime = DateTimeOffset.Now,
                        PluginName = item.Key,
                        ClassifyId = 0,
                        IsPublic = true,
                        UpdateUserId = 0,
                        CreateUserId = 0
                    });
                }
                else
                {
                    nativePlugins.Remove(toolPlugin);
                    nativePlugins.Add(new NativePluginInfo
                    {
                        PluginId = 0,
                        Description = item.Description,
                        Title = item.Key,
                        TemplatePluginClassify = item.Classify,
                        TemplatePluginKey = item.Key,
                        CreateTime = DateTimeOffset.Now,
                        PluginName = item.Name,
                        ClassifyId = toolPlugin.ClassifyId,
                        IsPublic = true,
                        UpdateUserId = 0,
                        CreateUserId = 0
                    });
                }
            }
        }

        await _mediator.Send(new FillUserInfoCommand { Items = nativePlugins });

        var classifyCount = await _databaseContext.Plugins.GroupBy(x => x.ClassifyId).Select(x => new KeyValue<string, int>
        {
            Key = x.Key.ToString(),
            Value = x.Count()
        }).ToArrayAsync();

        var templateClassifyCount = await _databaseContext.PluginNatives.GroupBy(x => x.TemplatePluginClassify).Select(x => new KeyValue<string, int>
        {
            Key = x.Key,
            Value = x.Count()
        }).ToArrayAsync();

        return new QueryNativePluginListCommandResponse
        {
            Items = nativePlugins,
            ClassifyCount = classifyCount,
            TemplateClassifyCount = templateClassifyCount
        };
    }
}
