using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Storage.Commands;
using MoAI.Storage.Models;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 完成文件上传，私有和公有文件都可以使用.
/// </summary>
[ApiController]
[Route("/storage")]
[EndpointGroupName("storage")]
public class CompleteUploadController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteUploadController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR.</param>
    public CompleteUploadController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 完成上传回调.
    /// </summary>
    /// <param name="req">上传完成请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>完成响应，包含文件公开访问地址.</returns>
    [HttpPost("complate_url")]
    public async Task<CompleteFileUploadCommandResponse> Post([FromBody] CompleteFileUploadCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}
