using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using MoAI.AI.MemoryDb;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
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
        var aiModel = await _databaseContext.Wikis
        .Where(x => x.Id == request.WikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new AiEndpoint
        {
            Name = x.Name,
            DeploymentName = x.DeploymentName,
            Title = x.Title,
            AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
            Provider = x.AiProvider.JsonToObject<AiProvider>(),
            ContextWindowTokens = x.ContextWindowTokens,
            Endpoint = x.Endpoint,
            Abilities = new ModelAbilities
            {
                Files = x.Files,
                FunctionCall = x.FunctionCall,
                ImageOutput = x.ImageOutput,
                Vision = x.IsVision,
            },
            MaxDimension = x.MaxDimension,
            TextOutput = x.TextOutput,
            Key = x.Key,
        }).FirstOrDefaultAsync();

        if (aiModel == null)
        {
            throw new BusinessException("知识库未配置嵌入模型") { StatusCode = 400 };
        }

        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiModel, 500);
        var memoryClient = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        if (request.IsDeleteDocuments)
        {
            await DeleteDocumentsAsync(request, memoryClient, cancellationToken);
        }

        // 删除配置
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginDocumentStates.Where(x => x.WikiId == request.WikiId && x.ConfigId == request.ConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigs.Where(x => x.Id == request.ConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WorkerTasks.Where(x => x.BindType == "wiki" && x.BindId == request.ConfigId));

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task DeleteDocumentsAsync(DeleteWikiPluginConfigCommand request, IMemoryDb memoryDb, CancellationToken cancellationToken)
    {
        var webDocumentIds = await _databaseContext.WikiPluginDocumentStates.Where(x => x.WikiId == request.WikiId).Select(x => x.WikiDocumentId)
            .ToArrayAsync();

        var documents = await _databaseContext.WikiDocuments
            .Where(x => webDocumentIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        _databaseContext.WikiDocuments.RemoveRange(documents);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        foreach (var document in documents)
        {
            await memoryDb.DeleteAsync(index: document.WikiId.ToString(), new Microsoft.KernelMemory.MemoryStorage.MemoryRecord
            {
                Id = document.Id.ToString()
            });

            // 删除 oss 文件
            await _mediator.Send(new DeleteFileCommand { FileIds = new[] { document.FileId } });
        }
    }
}
