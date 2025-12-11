#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Chunkers;
using Microsoft.KernelMemory.Handlers;
using MimeKit;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Handlers;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// <inheritdoc cref="UpdateWikiDocumentTextPartitionCommand"/>
/// </summary>
public class UpdateWikiDocumentTextPartitionCommandHandler : IRequestHandler<UpdateWikiDocumentTextPartitionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiDocumentTextPartitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiDocumentTextPartitionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiDocumentTextPartitionCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.WikiDocumentSliceContentPreviews
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId)
            .ExecuteDeleteAsync(cancellationToken);

        if (request == null || request.Items.Count == 0)
        {
            return EmptyCommandResponse.Default;
        }

        List<WikiDocumentSliceContentPreviewEntity> entities = new List<WikiDocumentSliceContentPreviewEntity>();

        int i = 0;
        foreach (var item in request.Items)
        {
            entities.Add(new WikiDocumentSliceContentPreviewEntity
            {
                WikiId = request.WikiId,
                DocumentId = request.DocumentId,
                SliceContent = item.Text,
                SliceOrder = i,
                SliceLength = item.Text.Length
            });

            i++;
        }

        await _databaseContext.WikiDocumentSliceContentPreviews.AddRangeAsync(entities, cancellationToken);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}