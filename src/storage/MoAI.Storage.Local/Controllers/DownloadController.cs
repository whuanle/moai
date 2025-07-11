// <copyright file="DownloadController.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Storage.Helpers;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 下载文件.
/// </summary>
[Route($"/download")]
[AllowAnonymous]
public class DownloadController : ControllerBase
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadController"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public DownloadController(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <summary>
    /// 下载文件.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="key"></param>
    /// <param name="expiry"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    [HttpGet("private/{filename}")]
    public IActionResult DownloadPrivateFile([FromRoute] string filename, [FromQuery] string key, [FromQuery] long expiry, [FromQuery] string token)
    {
        var text = $"{filename}|{key}|{expiry}";

        if (token != HashHelper.ComputeSha256Hash(text))
        {
            return Unauthorized("Invalid token.");
        }

        if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - expiry > 0)
        {
            return Unauthorized("Token has expired.");
        }

        var filePath = Path.Combine(_systemOptions.FilePath, "private", key);

#pragma warning disable CA3003 // 查看文件路径注入漏洞的代码
#pragma warning disable CA2000 // 丢失范围之前释放对象

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound($"File '{filename}' not found.");
        }

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = FileStoreHelper.GetMimeType(filePath);

        return File(fileStream, contentType, filename, enableRangeProcessing: true);
    }
}