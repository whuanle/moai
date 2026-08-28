using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using MoAI.Infra.Helpers;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

namespace MoAI.Storage.Middlewares;

/// <summary>
/// 静态文件访问中间件，将 /static/{objectKey} 请求中转至 OSS 并返回文件流.
/// <para>
/// 用户访问免登录静态地址，由后端从 OSS 读取文件并返回，前端无需接触 OSS 或拼接签名.
/// </para>
/// </summary>
public class StorageStaticFilesMiddleware : IMiddleware
{
    private const string RoutePrefix = StorageService.StaticRoutePrefix;

    private readonly IStorageService _storageService;
    private readonly ILogger<StorageStaticFilesMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageStaticFilesMiddleware"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="logger">日志.</param>
    public StorageStaticFilesMiddleware(IStorageService storageService, ILogger<StorageStaticFilesMiddleware> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value;
        if (path == null || !path.StartsWith($"{RoutePrefix}/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.Request.Method != HttpMethods.Get && context.Request.Method != HttpMethods.Head)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var objectKey = Uri.UnescapeDataString(path[RoutePrefix.Length..].TrimStart('/'));
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 仅允许中转公开目录（public）下的文件，其他一律拒绝访问
        if (!FileStoreHelper.IsPublicObjectKey(objectKey))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        try
        {
            var result = await _storageService.ReadAsync(objectKey, context.RequestAborted);

            var contentType = string.IsNullOrWhiteSpace(result.ContentType)
                ? "application/octet-stream"
                : result.ContentType;

            context.Response.ContentType = contentType;
            context.Response.ContentLength = result.FileSize > 0 ? result.FileSize : null;

            // 静态资源允许缓存 24 小时
            context.Response.Headers.CacheControl = "public,max-age=86400";
            context.Response.GetTypedHeaders().ETag = new EntityTagHeaderValue($"\"{HashHelper.ComputeSha256Hash(result.ObjectKey)}\"");

            if (context.Request.Method == HttpMethods.Head)
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            await result.FileStream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "读取静态文件 '{ObjectKey}' 失败", objectKey);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取静态文件 '{ObjectKey}' 发生异常", objectKey);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }
}
