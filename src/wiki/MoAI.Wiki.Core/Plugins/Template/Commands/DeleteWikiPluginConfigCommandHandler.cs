using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.MemoryDb;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Documents.Handlers;
using MoAI.Wiki.Models;
using System.Transactions;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// <inheritdoc cref="DeleteWikiPluginConfigCommand"/>
/// </summary>
public class DeleteWikiPluginConfigCommandHandler : IRequestHandler<DeleteWikiPluginConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiPluginConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="mediator"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="aiClientBuilder"></param>
    public DeleteWikiPluginConfigCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator, IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _mediator = mediator;
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiPluginConfigCommand request, CancellationToken cancellationToken)
    {
        var wikiPluginConfigEntity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (wikiPluginConfigEntity == null)
        {
            throw new BusinessException("未找到知识库配置");
        }

        if (wikiPluginConfigEntity.WorkState == (int)WorkerState.Processing || wikiPluginConfigEntity.WorkState == (int)WorkerState.Wait)
        {
            throw new BusinessException("当前已有任务在运行中，请先取消任务") { StatusCode = 400 };
        }

        if (request.IsDeleteDocuments)
        {
            var documents = await _databaseContext.WikiPluginConfigDocuments
                .Where(x => x.WikiId == request.WikiId && x.ConfigId == request.ConfigId)
                .Select(x => x.WikiDocumentId).ToArrayAsync();

            foreach (var item in documents)
            {
                await _mediator.Send(
                    new DeleteWikiDocumentCommand
                {
                    WikiId = request.WikiId,
                    DocumentId = item
                },
                    cancellationToken);
            }
        }

        // 删除 wiki_plugin_config_document_state、wiki_plugin_config_document、wiki_plugin_config
        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        // 删除配置
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigDocumentStates.Where(x => x.WikiId == request.WikiId && x.ConfigId == request.ConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigDocuments.Where(x => x.Id == request.ConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigs.Where(x => x.Id == request.ConfigId));

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
