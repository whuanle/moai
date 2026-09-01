using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Models;

namespace MoAI.Storage.Services;

/// <summary>
/// 文件存储领域服务.
/// <para>
/// 其他模块只能通过该服务访问文件，不允许直接操作 file 表或访问 OSS.
/// </para>
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// 公开静态文件访问路由前缀，静态文件中间件与公开 URL 拼接共用此常量.
    /// </summary>
    public const string StaticRoutePrefix = "/static";

    /// <summary>
    /// 生成文件预上传地址，并登记 file 记录（幂等：根据 ObjectKey 去重）.
    /// </summary>
    /// <param name="command">预上传命令.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>预上传响应.</returns>
    Task<PreUploadFileCommandResponse> PreUploadAsync(PreUploadFileCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以流的方式上传文件并登记 file 记录（幂等：根据 ObjectKey 去重）.
    /// </summary>
    /// <param name="command">上传命令.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>上传结果.</returns>
    Task<FileUploadResult> UploadStreamAsync(UploadStreamFileCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成文件上传，校验文件是否完整并标记已上传.
    /// </summary>
    /// <param name="fileId">文件 id.</param>
    /// <param name="isSuccess">上传是否成功.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件 ObjectKey.</returns>
    Task<string> CompleteAsync(long fileId, bool isSuccess, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除文件（软删除 file 记录并清理 OSS 对象）.
    /// </summary>
    /// <param name="fileIds">文件 id 列表.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task DeleteFilesAsync(IReadOnlyCollection<long> fileIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成文件下载地址（预签名）.
    /// </summary>
    /// <param name="objectKeys">对象 key 列表.</param>
    /// <param name="expiryDuration">地址有效期.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>key 到下载地址的映射.</returns>
    Task<IReadOnlyDictionary<string, Uri>> GetDownloadUrlsAsync(IReadOnlyCollection<KeyValueString> objectKeys, TimeSpan expiryDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成单个文件下载地址（预签名）.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="fileName">文件名称.</param>
    /// <param name="expiryDuration">地址有效期.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>下载地址.</returns>
    Task<Uri> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan expiryDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 文件是否存在.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否存在.</returns>
    Task<bool> FileExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取文件流，供静态资源中转与内部处理使用.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件流及元数据.</returns>
    Task<StorageFileReadResult> ReadAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件公开静态访问地址（免登录、静态地址，经 /static 中间件中转）.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <returns>公开访问地址.</returns>
    Uri GetPublicFileUrl(string objectKey);

    /// <summary>
    /// 获取 file 记录信息.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件信息，不存在时返回 null.</returns>
    Task<StorageFileInfo?> GetFileInfoAsync(string objectKey, CancellationToken cancellationToken = default);
}
