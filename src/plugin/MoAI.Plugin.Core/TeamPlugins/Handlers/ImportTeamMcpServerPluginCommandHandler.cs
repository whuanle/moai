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
using MoAI.Plugin.CustomPlugins.Templates.Commands;
using MoAI.Plugin.Models;
using MoAI.Plugin.TeamPlugins.Commands;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MoAI.Plugin.TeamPlugins.Handlers;

/// <summary>
/// <inheritdoc cref="ImportTeamMcpServerPluginCommand"/>
/// </summary>
public class ImportTeamMcpServerPluginCommandHandler : IRequestHandler<ImportTeamMcpServerPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportTeamMcpServerPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ImportTeamMcpServerPluginCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task<SimpleInt> Handle(ImportTeamMcpServerPluginCommand request, CancellationToken cancellationToken)
    {
        return _mediator.Send(new TemplateImportMcpServerPluginCommand
        {
            Description = request.Description,
            ClassifyId = request.ClassifyId,
            IsPublic = false,
            Header = request.Header,
            Name = request.Name,
            Query = request.Query,
            ServerUrl = request.ServerUrl,
            TeamId = request.TeamId,
            Title = request.Title,
        });
    }
}
