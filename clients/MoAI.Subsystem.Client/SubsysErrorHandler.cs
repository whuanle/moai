namespace MoAI.Subsystem.Client;

/// <summary>
/// 错误转换处理器：非 2xx 响应读取原始错误体并抛出 <see cref="SubsysException"/>（保留服务端 error.type/error.code 语义）。
/// 无认证依赖，用于 token 管道（避免与 token 管理器持锁重入）。
/// </summary>
public class SubsysErrorHandler : SubsysHandlerBase
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await BufferRequestContentAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        return await EnsureSuccessAsync(response, cancellationToken);
    }
}
