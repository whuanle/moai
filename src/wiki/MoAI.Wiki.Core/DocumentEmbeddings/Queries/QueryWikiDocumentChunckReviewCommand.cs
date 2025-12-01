using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Handlers;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// 文件切片预览服务.
/// </summary>
public class QueryWikiDocumentChunckReviewCommandHandler : IRequestHandler<QueryWikiDocumentChunckReviewCommand, QueryWikiDocumentChunckReviewCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    public QueryWikiDocumentChunckReviewCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<QueryWikiDocumentChunckReviewCommandResponse> Handle(QueryWikiDocumentChunckReviewCommand request, CancellationToken cancellationToken)
    {
        var document = await _databaseContext.WikiDocuments
            .Where(d => d.WikiId == request.WikiId && d.Id == request.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            throw new BusinessException("未找到文档文件");
        }

        var memoryBuilder = new KernelMemoryBuilder()
            .WithoutEmbeddingGenerator()
            .WithoutTextGenerator();

        var kmClient = memoryBuilder.Build();

        var serviceProvider = memoryBuilder.Services.BuildServiceProvider();
        using var textExtractionHandler = ActivatorUtilities.CreateInstance<TextExtractionHandler>(serviceProvider, "extract");


        return default!;
    }
}
