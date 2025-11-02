using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MoAI.Common.Queries;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Storage.Helper;
using System.IO.Pipelines;
using System.Net;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 兼容 S3 接口的文件上传接口.
/// </summary>
[Route("/api/storage")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    public UploadController(IMediator mediator, SystemOptions systemOptions)
    {
        _mediator = mediator;
        _systemOptions = systemOptions;
    }

    /// <summary>
    /// 上传私有文件.
    /// </summary>
    /// <param name="objectPath"></param>
    /// <param name="token"></param>
    /// <param name="expiry"></param>
    /// <param name="size"></param>
    /// <param name="contentType"></param>
    /// <param name="contentLength"></param>
    /// <returns></returns>
    [HttpPut("upload/{objectPath}")]
    public async Task<IActionResult> UploadPrivateFile(
        [FromRoute] string objectPath,
        [FromQuery] string token,
        [FromQuery] ulong expiry,
        [FromQuery] int size,
        [FromHeader(Name = "Content-Type")] string contentType,
        [FromHeader(Name = "Content-Length")] int contentLength)
    {
        // 已过期
        if (DateTimeOffset.FromUnixTimeMilliseconds((long)expiry) < DateTimeOffset.Now)
        {
            return Unauthorized("Have expired");
        }

        var objectKey = WebUtility.UrlDecode(encodedValue: objectPath);

        var text = $"{objectKey}|{expiry}|{size}|{contentType}";
        var sumToken = HashHelper.ComputeSha256Hash(text);

        if (token != sumToken)
        {
            return Unauthorized("Invalid token.");
        }

        var serverInfo = await _mediator.Send(new QueryServerInfoCommand());

        if (contentLength != size || contentLength == 0 || contentLength > serverInfo.MaxUploadFileSize)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes or file is empty.");
        }

        var filePath = Path.Combine(_systemOptions.Storage.LocalPath, objectKey);

#pragma warning disable CA3003 // 查看文件路径注入漏洞的代码
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        PipeReader contentReader = PipeReader.Create(Request.Body, new StreamPipeReaderOptions(leaveOpen: true));
        var cancellationToken = HttpContext.RequestAborted;

        await FileUploadHelper.SaveViaPipeReaderAsync(filePath, contentLength, contentReader, cancellationToken);

        return Ok();
    }
}
