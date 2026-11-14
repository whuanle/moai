using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件文件存储领域服务.
/// <para>
/// 为插件提供与底层存储实现解耦的文件能力：保存流/字节/公网文件并返回文件 id，
/// 再通过文件 id 获取下载地址或公开访问地址。插件只需依赖 <c>MoAI.AIPlugin.Shared</c>，
/// 不需要引用存储（S3）模块，也不允许直接操作 file 表或 OSS.
/// </para>
/// </summary>
public interface IPluginFileStorageService
{
    /// <summary>
    /// 保存文件流，返回文件 id.
    /// <para>
    /// 文件按内容 SHA-256 寻址，相同内容重复保存会复用同一条文件记录。
    /// </para>
    /// </summary>
    /// <param name="stream">文件流；方法不会关闭调用方的流.</param>
    /// <param name="options">保存选项.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件 id.</returns>
    Task<long> SaveAsync(Stream stream, PluginFileSaveOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存字节内容，返回文件 id.
    /// </summary>
    /// <param name="content">文件内容.</param>
    /// <param name="options">保存选项.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件 id.</returns>
    Task<long> SaveAsync(byte[] content, PluginFileSaveOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载 http/https 公网文件并转存，返回文件 id.
    /// </summary>
    /// <param name="url">公网文件地址.</param>
    /// <param name="options">保存选项；<see cref="PluginFileSaveOptions.FileName"/> 留空时从响应头或地址自动识别.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件 id.</returns>
    Task<long> SaveFromUrlAsync(string url, PluginFileSaveOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按文件 id 获取文件信息.
    /// </summary>
    /// <param name="fileId">文件 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件信息；文件不存在时返回 null.</returns>
    Task<PluginStoredFile?> GetFileInfoAsync(long fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按文件 id 获取下载地址（预签名，存在有效期，适合导出 PDF/Word 等一次性下载）.
    /// </summary>
    /// <param name="fileId">文件 id.</param>
    /// <param name="fileName">下载时使用的文件名；留空时按文件 id 与扩展名生成.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>下载地址.</returns>
    Task<Uri> GetDownloadUrlAsync(long fileId, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按文件 id 获取公开访问地址（免登录静态地址，不会过期）.
    /// </summary>
    /// <param name="fileId">文件 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>公开访问地址；文件不存在或非公开文件时返回 null.</returns>
    Task<Uri?> GetPublicUrlAsync(long fileId, CancellationToken cancellationToken = default);
}
