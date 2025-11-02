using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Storage.Helpers;

namespace MoAI.Storage.Controllers;

/// <summary>
/// 下载文件.
/// </summary>
[Route($"/api")]
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
    [HttpGet("download/{filename}")]
    public IActionResult DownloadPrivateFile([FromRoute] string filename, [FromQuery] string key, [FromQuery] ulong expiry, [FromQuery] string token)
    {
        // 已过期
        if (DateTimeOffset.FromUnixTimeMilliseconds((long)expiry) < DateTimeOffset.Now)
        {
            return Unauthorized("Have expired");
        }

        var text = $"{expiry}|{key}|{filename}";

        if (token != HashHelper.ComputeSha256Hash(text))
        {
            return Unauthorized("Invalid token.");
        }

        var filePath = Path.Combine(_systemOptions.Storage.LocalPath, key);

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