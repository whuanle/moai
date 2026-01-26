using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Chat.Manager.Models;
using MoAI.App.Chat.Manager.Queries;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;

namespace MoAI.App.Manager.Queries;

/// <summary>
/// <inheritdoc cref="QueryChatAppConfigCommand"/>
/// </summary>
public class QueryChatAppConfigCommandHandler : IRequestHandler<QueryChatAppConfigCommand, QueryChatAppConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryChatAppConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryChatAppConfigCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryChatAppConfigCommandResponse> Handle(QueryChatAppConfigCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Join(
                _databaseContext.AppChatapps,
                app => app.Id,
                common => common.AppId,
                (app, common) => new
                {
                    App = app,
                    Common = common
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var response = new QueryChatAppConfigCommandResponse
        {
            AppId = result.App.Id,
            Name = result.App.Name,
            Description = result.App.Description,
            AvatarKey = result.App.Avatar,
            IsForeign = result.App.IsForeign,
            AppType = (AppType)result.App.AppType,
            IsDisable = result.App.IsDisable,
            IsPublic = result.App.IsPublic,
            Prompt = result.Common.Prompt,
            ModelId = result.Common.ModelId,
            ClassifyId = result.App.ClassifyId,
            WikiIds = result.Common.WikiIds.JsonToObject<IReadOnlyCollection<int>>() ?? Array.Empty<int>(),
            Plugins = result.Common.Plugins.JsonToObject<IReadOnlyCollection<string>>() ?? Array.Empty<string>(),
            ExecutionSettings = result.Common.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>(),
            IsAuth = result.App.IsAuth,
            CreateTime = result.App.CreateTime,
            UpdateTime = result.App.UpdateTime
        };

        await _mediator.Send(new QueryAvatarUrlCommand { Items = new[] { response } });

        return response;
    }
}
