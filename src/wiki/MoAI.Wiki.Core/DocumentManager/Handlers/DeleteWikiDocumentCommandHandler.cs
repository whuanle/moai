using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using MoAI.AI.MemoryDb;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Wiki.Events;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 删除知识库文档.
/// </summary>
public class DeleteWikiDocumentCommandHandler : IRequestHandler<DeleteWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="aiClientBuilder"></param>
    public DeleteWikiDocumentCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions, IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiDocumentCommand request, CancellationToken cancellationToken)
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

        // 删除数据库记录，附属表不需要删除
        var document = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && x.Id == request.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            throw new BusinessException("文档不存在") { StatusCode = 404 };
        }

        _databaseContext.WikiDocuments.Remove(document);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // todo: 要改
        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiModel, 500);
        var memoryClient = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        await memoryClient.DeleteAsync(index: document.WikiId.ToString(), new Microsoft.KernelMemory.MemoryStorage.MemoryRecord
        {
            Id = document.Id.ToString()
        });

        // 删除 oss 文件
        await _mediator.Send(new DeleteFileCommand { FileIds = new[] { document.FileId } });

        var documentCount = await _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId).CountAsync();
        if (documentCount == 0)
        {
            await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, false));
        }

        await _mediator.Publish(new DeleteWikiDocumentEvent
        {
            WikiId = request.WikiId,
            DocumentId = request.DocumentId
        });

        return EmptyCommandResponse.Default;
    }
}
