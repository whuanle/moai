using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 完成文件上传，私有和公有文件都可以使用.
/// </summary>
[FastEndpoints.HttpPost($"{ApiPrefix.Prefix}/complate_url")]
public class ComplateUploadEndpoint : Endpoint<ComplateFileUploadCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateUploadEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ComplateUploadEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override Task<EmptyCommandResponse> ExecuteAsync(ComplateFileUploadCommand req, CancellationToken ct)
        => _mediator.Send(req, ct);
}
