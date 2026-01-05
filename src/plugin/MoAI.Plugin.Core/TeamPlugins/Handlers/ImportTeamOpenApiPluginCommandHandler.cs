using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.TeamPlugins.Commands;
using MoAI.Storage.Commands;
using MoAI.Storage.Queries;
using MoAI.Storage.Queries.Response;
using MoAIPlugin.Core.Commands;
using System.Transactions;

namespace MoAI.Plugin.TeamPlugins.Handlers;

/// <summary>
/// <inheritdoc cref="ImportTeamOpenApiPluginCommand"/>
/// </summary>
public class ImportTeamOpenApiPluginCommandHandler : IRequestHandler<ImportTeamOpenApiPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportTeamOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ImportTeamOpenApiPluginCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task<SimpleInt> Handle(ImportTeamOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        return _mediator.Send(
            new TemplateImportOpenApiPluginCommand
            {
                FileId = request.FileId,
                FileName = request.FileName,
                Name = request.Name,
                Title = request.Title,
                Description = request.Description,
                ClassifyId = request.ClassifyId,
                IsPublic = false,
                TeamId = request.TeamId,
            },
            cancellationToken);
    }
}
