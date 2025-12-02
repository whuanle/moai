using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Template.Models;

namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// <see cref="QueryWikiPluginConfigCommand{T}"/>
/// </summary>
/// <typeparam name="T">.</typeparam>
public class QueryWikiPluginConfigCommandHandler<T> : IRequestHandler<QueryWikiPluginConfigCommand<T>, QueryWikiPluginrConfigCommandResponse<T>>
        where T : IWikiPluginKey
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiPluginConfigCommandHandler{T}"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiPluginConfigCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiPluginrConfigCommandResponse<T>> Handle(QueryWikiPluginConfigCommand<T> request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.WikiPluginConfigs
            .Where(x => x.Id == request.ConfigId && x.WikiId == request.WikiId)
            .Select(x => new
            {
                ConfigId = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
                Config = x.Config
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            throw new BusinessException("未找到知识库配置");
        }

        var config = result.Config.JsonToObject<T>()!;
        var responseConfig = new QueryWikiPluginrConfigCommandResponse<T>
        {
            ConfigId = result.ConfigId,
            Title = result.Title,
            WikiId = result.WikiId,
            CreateTime = result.CreateTime,
            CreateUserId = result.CreateUserId,
            UpdateUserId = result.UpdateUserId,
            UpdateTime = result.UpdateTime,
            Config = config
        };

        await _mediator.Send(new FillUserInfoCommand { Items = new[] { responseConfig } });

        return responseConfig;
    }
}
