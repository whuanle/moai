// <copyright file="UploadController.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MoAI.Infra.Service;
using MoAI.Store.Services;

namespace MoAI.Storage.Controllers;

[Route($"/api/storage/upload")]
public class UploadController : ControllerBase
{
    private readonly IAESProvider _aesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadController"/> class.
    /// </summary>
    /// <param name="aesProvider"></param>
    public UploadController(IAESProvider aesProvider)
    {
        _aesProvider = aesProvider;
    }

    [HttpPut("public/{objectKey}")]
    public IActionResult UploadPublicFile(
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
        var newTokenEncode = _aesProvider.Encrypt(newToken);
        if (newToken != token)
        {
            return new UnauthorizedResult();
        }

        if (contentLength > size)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes.");
        }

        // 检查文件是否存在，如果存在则删除并覆盖
        return new OkObjectResult($"Public file '{objectKey}' uploaded successfully.");
    }

    [HttpPut("private/{objectKey}")]
    public IActionResult UploadPrivateFile(
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
        var newTokenEncode = _aesProvider.Encrypt(newToken);
        if (newToken != token)
        {
            return new UnauthorizedResult();
        }

        if (contentLength > size)
        {
            return new BadRequestObjectResult($"File size exceeds the limit of {size} bytes.");
        }

        return new OkObjectResult($"Public file '{objectKey}' uploaded successfully.");
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="publicFileStorage"></param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpGet("/test")]
    public static async Task<string> TestAsync([FromServices] IPublicFileStorage publicFileStorage)
    {
        var type = MimeTypes.GetMimeType(@"C:\Users\ASUS\Pictures\6f167d2a296f33bfa54d78d2a61f106a_1.jpg");
        var yrl = await publicFileStorage.GeneratePreSignedUploadUrlAsync(new FileObject
        {
            ObjectKey = "aaa.jpg",
            ExpiryDuration = TimeSpan.FromHours(1),
            ContentType = "image/jpeg",
            MaxFileSize = 58005
        });

        return yrl;
    }
}
