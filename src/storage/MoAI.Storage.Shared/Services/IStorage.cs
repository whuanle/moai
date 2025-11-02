using MoAI.Infra.Models;
using MoAI.Store.Services;

namespace MoAI.Storage.Services;

/// <summary>
/// 存储接口.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// 批量删除文件
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns></returns>
    Task DeleteFilesAsync(IEnumerable<string> objectKeys);

    /// <summary>
    /// 下载文件到本地位置.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="filePath"></param>
    /// <returns></returns>
    Task DownloadAsync(string objectKey, string filePath);

    /// <summary>
    /// 检测文件是否存在.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <returns></returns>
    Task<bool> FileExistsAsync(string objectKey);

    /// <summary>
    /// 检测文件是否存在.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns></returns>
    Task<IReadOnlyDictionary<string, bool>> FilesExistsAsync(IEnumerable<string> objectKeys);

    /// <summary>
    /// 构建预上传地址.
    /// </summary>
    /// <param name="fileObject"></param>
    /// <returns></returns>
    Task<string> GeneratePreSignedUploadUrlAsync(FileObject fileObject);

    /// <summary>
    /// 读取文件大小.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns></returns>
    Task<IReadOnlyDictionary<string, long>> GetFileSizeAsync(IEnumerable<string> objectKeys);

    /// <summary>
    /// 读取文件大小.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <returns></returns>
    Task<long> GetFileSizeAsync(string objectKey);

    /// <summary>
    /// 获取文件下载地址.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <param name="expiryDuration"></param>
    /// <returns></returns>
    Task<IReadOnlyDictionary<string, Uri>> GetFilesUrlAsync(IReadOnlyCollection<KeyValueString> objectKeys, TimeSpan expiryDuration);

    /// <summary>
    /// 获取文件下载地址.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="fileName"></param>
    /// <param name="expiryDuration"></param>
    /// <returns></returns>
    Task<Uri> GetFileUrlAsync(string objectKey, string fileName, TimeSpan expiryDuration);

    /// <summary>
    /// 以流的方式上传文件.
    /// </summary>
    /// <param name="inputStream"></param>
    /// <param name="objectKey"></param>
    /// <returns></returns>
    Task UploadFileAsync(Stream inputStream, string objectKey);
}
