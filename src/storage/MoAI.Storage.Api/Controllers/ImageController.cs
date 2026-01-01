using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 图片存储相关接，团队头像、知识库头像等都可以使用.
/// </summary>
[ApiController]
[Route("/storage/image")]
public class ImageController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR.</param>
    public ImageController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 获取图片预上传地址.
    /// </summary>
    /// <param name="req">预上传请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>预上传响应，包含上传地址和文件信息.</returns>
    [HttpPost("pre_upload")]
    public async Task<PreUploadImageCommandResponse> PreUpload([FromBody] PreUploadImageCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}
