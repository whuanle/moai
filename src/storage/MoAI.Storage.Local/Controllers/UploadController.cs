// <copyright file="UploadController.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Public.Queries;
using MoAI.Storage.Helper;
using System.IO.Pipelines;
using System.Net;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 文件上传.
/// </summary>
[Route($"/api/storage/upload")]
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
    /// 上传公有文件.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="token"></param>
    /// <param name="expiry"></param>
    /// <param name="size"></param>
    /// <param name="contentType"></param>
    /// <param name="contentLength"></param>
    /// <returns></returns>
    [HttpPut("public/{objectKey}")]
    public async Task<IActionResult> UploadPublicFile(
        [FromRoute] string objectKey,
        [FromQuery] string token,
        [FromQuery] string expiry,
        [FromQuery] int size,
        [FromHeader(Name = "Content-Type")] string contentType,
        [FromHeader(Name = "Content-Length")] int contentLength)
    {
        objectKey = WebUtility.UrlDecode(encodedValue: objectKey);

        var text = $"{objectKey}|{expiry}|{size}|{contentType}";
        var sumToken = HashHelper.ComputeSha256Hash(text);

        if (token != sumToken)
        {
            return new UnauthorizedResult();
        }

        var serverInfo = await _mediator.Send(new QueryServerInfoCommand());

        if (contentLength != size || contentLength == 0 || contentLength > serverInfo.MaxUploadFileSize)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes or file is empty.");
        }

        var filePath = Path.Combine(_systemOptions.FilePath, "public", objectKey);

        PipeReader contentReader = PipeReader.Create(Request.Body, new StreamPipeReaderOptions(leaveOpen: true));
        var cancellationToken = HttpContext.RequestAborted;

        await FileUploadHelper.SaveViaPipeReaderAsync(filePath, contentLength, contentReader,  cancellationToken);

        return Ok();
    }

    /// <summary>
    /// 上传私有文件.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="token"></param>
    /// <param name="expiry"></param>
    /// <param name="size"></param>
    /// <param name="contentType"></param>
    /// <param name="contentLength"></param>
    /// <returns></returns>
    [HttpPut("private/{objectKey}")]
    public async Task<IActionResult> UploadPrivateFile(
        [FromRoute] string objectKey,
        [FromQuery] string token,
        [FromQuery] string expiry,
        [FromQuery] int size,
        [FromHeader(Name = "Content-Type")] string contentType,
        [FromHeader(Name = "Content-Length")] int contentLength)
    {
        objectKey = WebUtility.UrlDecode(encodedValue: objectKey);

        var text = $"{objectKey}|{expiry}|{size}|{contentType}";
        var sumToken = HashHelper.ComputeSha256Hash(text);

        if (token != sumToken)
        {
            return new UnauthorizedResult();
        }

        var serverInfo = await _mediator.Send(new QueryServerInfoCommand());

        if (contentLength != size || contentLength == 0 || contentLength > serverInfo.MaxUploadFileSize)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes or file is empty.");
        }

        var filePath = Path.Combine(_systemOptions.FilePath, "private", objectKey);

        PipeReader contentReader = PipeReader.Create(Request.Body, new StreamPipeReaderOptions(leaveOpen: true));
        var cancellationToken = HttpContext.RequestAborted;

        await FileUploadHelper.SaveViaPipeReaderAsync(filePath, contentLength, contentReader, cancellationToken);

        return Ok();
    }
}
