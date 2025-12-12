using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiPluginConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="mediator"></param>
    /// <param name="serviceProvider"></param>
    public DeleteWikiPluginConfigCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator, IServiceProvider serviceProvider)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _mediator = mediator;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiPluginConfigCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        if (request.IsDeleteDocuments)
        {
            await DeleteDocumentsAsync(request, cancellationToken);
        }

        // 删除配置
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginDocumentStates.Where(x => x.WikiId == request.WikiId && x.ConfigId == request.ConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginConfigs.Where(x => x.Id == request.ConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WorkerTasks.Where(x => x.BindType == "wiki" && x.BindId == request.ConfigId));

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task DeleteDocumentsAsync(DeleteWikiPluginConfigCommand request, CancellationToken cancellationToken)
    {
        var webDocumentIds = await _databaseContext.WikiPluginDocumentStates.Where(x => x.WikiId == request.WikiId).Select(x => x.WikiDocumentId)
            .ToArrayAsync();

        var documents = await _databaseContext.WikiDocuments
            .Where(x => webDocumentIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        _databaseContext.WikiDocuments.RemoveRange(documents);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var memoryDb = _serviceProvider.GetKeyedService<IMemoryDbClient>(_systemOptions.Wiki.DBType);
        if (memoryDb == null)
        {
            throw new BusinessException("不支持的文档数据库");
        }

        IKernelMemoryBuilder memoryBuilder = new KernelMemoryBuilder();
        memoryBuilder = memoryDb.Configure(memoryBuilder, _systemOptions.Wiki.ConnectionString);

        // 删除文档向量
        var memoryClient = memoryBuilder
            .WithSimpleFileStorage(Path.GetTempPath())
            .WithoutEmbeddingGenerator()
            .WithoutTextGenerator()
            .Build();

        foreach (var document in documents)
        {
            await memoryClient.DeleteDocumentAsync(documentId: document.Id.ToString(), index: document.WikiId.ToString());

            // 删除 oss 文件
            await _mediator.Send(new DeleteFileCommand { FileIds = new[] { document.FileId } });
        }
    }
}
