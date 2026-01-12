using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 公共存储相关接口，团队头像、知识库头像、临时文件等都可以使用.
/// </summary>
[ApiController]
[Route("/storage/public")]
[EndpointGroupName("storage")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR.</param>
    public PublicController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 获取图片预上传地址.
    /// </summary>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>预上传响应，包含上传地址和文件信息.</returns>
    [HttpPost("pre_upload_image")]
    public async Task<PreUploadImageCommandResponse> PreUpload([FromBody] PreUploadImageCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取临时文件预上传地址.
    /// </summary>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>预上传响应，包含上传地址和文件信息.</returns>
    [HttpPost("pre_upload_temp")]
    public async Task<PreUploadTempFileCommandResponse> PreUploadTemp([FromBody] PreUploadTempFileCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}
