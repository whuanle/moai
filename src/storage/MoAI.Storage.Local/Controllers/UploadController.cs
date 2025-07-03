using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MoAI.Infra.Service;
using MoAI.Store.Services;

namespace MoAI.Storage.Controllers;

[Route($"/api/storage/upload")]
public class UploadController : ControllerBase
{
    private readonly IAESProvider _aesProvider;

    public UploadController(IAESProvider aesProvider)
    {
        _aesProvider = aesProvider;
    }

    [HttpPut("public/{objectKey}")]
    public async Task<IActionResult> UploadPublicFileAsync(
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
    public async Task<IActionResult> UploadPrivateFileAsync(
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

        //

        return new OkObjectResult($"Public file '{objectKey}' uploaded successfully.");
    }

    [HttpGet("/test")]
    public async Task<string> TestAsync([FromServices] IPublicFileStorage publicFileStorage)
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
