using MediatR;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.Documents.Handlers;

public class WikiDocumentTextPartitionPreviewCommandHandler : IRequestHandler<WikiDocumentTextPartitionPreviewCommand, WikiDocumentTextPartitionPreviewCommandResponse>
{
    public async Task<WikiDocumentTextPartitionPreviewCommandResponse> Handle(WikiDocumentTextPartitionPreviewCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}