using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.CustomPlugins.Templates.Commands;
using MoAI.Plugin.Models;
using MoAI.Plugin.TeamPlugins.Commands;
using MoAI.Plugin.TeamPlugins.Handlers;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MoAIPlugin.Core.Handlers;

/// <summary>
/// <inheritdoc cref="ImportMcpServerPluginCommand"/>
/// </summary>
public class ImportMcpServerCommandHandler : IRequestHandler<ImportMcpServerPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportMcpServerCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ImportMcpServerCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task<SimpleInt> Handle(ImportMcpServerPluginCommand request, CancellationToken cancellationToken)
    {
        return _mediator.Send(new TemplateImportMcpServerPluginCommand
        {
            Description = request.Description,
            ClassifyId = request.ClassifyId,
            IsPublic = request.IsPublic,
            Header = request.Header,
            Name = request.Name,
            Query = request.Query,
            ServerUrl = request.ServerUrl,
            TeamId = null,
            Title = request.Title,
        });
    }
}