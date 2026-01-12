using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 完成文件上传，私有和公有文件都可以使用.
/// </summary>
[ApiController]
[Route("/storage")]
[EndpointGroupName("storage")]
public class ComplateUploadController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateUploadController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ComplateUploadController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 完成上传回调.
    /// POST {ApiPrefix.Prefix}/complate_url
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns>.</returns>
    [HttpPost("complate_url")]
    public async Task<EmptyCommandResponse> Post([FromBody] ComplateFileUploadCommand req, CancellationToken ct)
    {
        var result = await _mediator.Send(req, ct);
        return result;
    }
}