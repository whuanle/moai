// <copyright file="UploadController.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MoAI.Infra;
using MoAI.Infra.Service;
using MoAI.Store.Services;
using System.Net;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 文件上传.
/// </summary>
[Route($"/api/storage/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAESProvider _aesProvider;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;
    private readonly IPrivateFileStorage _privateFileStorage;
    private readonly IPublicFileStorage _publicFileStorage;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadController"/> class.
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    /// <param name="aesProvider"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    /// <param name="privateFileStorage"></param>
    /// <param name="publicFileStorage"></param>
    public UploadController(IHttpContextAccessor httpContextAccessor, IAESProvider aesProvider, IMediator mediator, SystemOptions systemOptions, IPrivateFileStorage privateFileStorage, IPublicFileStorage publicFileStorage)
    {
        _httpContextAccessor = httpContextAccessor;
        _aesProvider = aesProvider;
        _mediator = mediator;
        _systemOptions = systemOptions;
        _privateFileStorage = privateFileStorage;
        _publicFileStorage = publicFileStorage;
    }

    /// <summary>
    /// 上传公有文件.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="token"></param>
    /// <param name="expires"></param>
    /// <param name="date"></param>
    /// <param name="size"></param>
    /// <param name="contentType"></param>
    /// <param name="contentLength"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPut("public/{objectKey}")]
    public async Task<IActionResult> UploadPublicFile(
        [FromRoute] string objectKey,
        [FromQuery] string token,
        [FromQuery] string expires,
        [FromQuery] string date,
        [FromQuery] int size,
        [FromHeader(Name = "Content-Type")] string contentType,
        [FromHeader(Name = "Content-Length")] int contentLength,
        CancellationToken ct)
    {
        var newToken = $"{objectKey}|{expires}|{date}|{size}|{contentType}";
        var newTokenEncode = WebUtility.UrlEncode(_aesProvider.Encrypt(newToken));
        if (newToken != token)
        {
            return new UnauthorizedResult();
        }

        if (contentLength > size)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes.");
        }

        // 判断路径和文件是否存在
        // 如果文件大小不一致，则删除以前的
        // 上传保存文件

        // 读取 body 流
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return BadRequest("Request context is not available.");
        }

        if (!request.Body.CanSeek)
        {
            return BadRequest("Request body stream must be seekable.");
        }

        request.Body.Seek(0, SeekOrigin.Begin);

        await _publicFileStorage.UploadFileAsync(request.Body, objectKey);

        return Ok();
    }

    /// <summary>
    /// 上传私有文件.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="token"></param>
    /// <param name="expires"></param>
    /// <param name="date"></param>
    /// <param name="size"></param>
    /// <param name="contentType"></param>
    /// <param name="contentLength"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPut("private/{objectKey}")]
    public async Task<IActionResult> UploadPrivateFile(
        [FromRoute] string objectKey,
        [FromQuery] string token,
        [FromQuery] string expires,
        [FromQuery] string date,
        [FromQuery] int size,
        [FromHeader(Name = "Content-Type")] string contentType,
        [FromHeader(Name = "Content-Length")] int contentLength,
        CancellationToken ct)
    {
        var newToken = $"{objectKey}|{expires}|{date}|{size}|{contentType}";
        var newTokenEncode = WebUtility.UrlEncode(_aesProvider.Encrypt(newToken));
        if (newToken != token)
        {
            return new UnauthorizedResult();
        }

        if (contentLength > size)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes.");
        }

        // 判断路径和文件是否存在
        // 如果文件大小不一致，则删除以前的
        // 上传保存文件

        // 读取 body 流
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return BadRequest("Request context is not available.");
        }

        if (!request.Body.CanSeek)
        {
            return BadRequest("Request body stream must be seekable.");
        }

        request.Body.Seek(0, SeekOrigin.Begin);

        await _privateFileStorage.UploadFileAsync(request.Body, objectKey);

        return Ok();
    }
}
