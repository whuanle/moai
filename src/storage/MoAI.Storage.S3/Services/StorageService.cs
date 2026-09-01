using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.Storage.Services;

/// <summary>
/// 文件存储领域服务实现.
/// </summary>
    public class StorageService : IStorageService
    {
        /// <summary>
        /// 公开静态文件访问路由前缀.
        /// </summary>
        public const string StaticRoutePrefix = "/static";

        /// <summary>
        /// 预上传记录有效期，超过该时长仍未完成上传的记录将被废弃.
        /// </summary>
        private static readonly TimeSpan PreUploadTimeout = TimeSpan.FromMinutes(5);

    private readonly DatabaseContext _databaseContext;
    private readonly S3Client _s3Client;
    private readonly SystemOptions _systemOptions;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="s3Client">S3 客户端.</param>
    /// <param name="systemOptions">系统配置.</param>
    /// <param name="userContextProvider">用户上下文.</param>
    public StorageService(DatabaseContext databaseContext, S3Client s3Client, SystemOptions systemOptions, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _s3Client = s3Client;
        _systemOptions = systemOptions;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<PreUploadFileCommandResponse> PreUploadAsync(PreUploadFileCommand command, CancellationToken cancellationToken = default)
    {
        var fileEntity = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.ObjectKey == command.ObjectKey, cancellationToken);

        // 文件已上传完成，直接复用
        if (fileEntity != null && fileEntity.IsUploaded)
        {
            return new PreUploadFileCommandResponse
            {
                IsExist = true,
                FileId = fileEntity.Id,
                ObjectKey = fileEntity.ObjectKey
            };
        }

        if (fileEntity == null)
        {
            fileEntity = CreateFileEntity(command);
            await _databaseContext.Files.AddAsync(fileEntity, cancellationToken);
        }
        else if (IsExpired(fileEntity))
        {
            // 预上传记录已过期（长时间未完成上传或上传失败残留），废弃旧记录后重新创建
            await AbandonFileAsync(fileEntity, cancellationToken);
            fileEntity = CreateFileEntity(command);
            await _databaseContext.Files.AddAsync(fileEntity, cancellationToken);
        }
        else
        {
            // 已有预上传记录但未上传，复用相同的 id
            fileEntity.UpdateTime = DateTimeOffset.Now;
            _databaseContext.Files.Update(fileEntity);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        var uploadUrl = await _s3Client.GeneratePreSignedUploadUrlAsync(fileEntity.ObjectKey, command.ContentType, command.Expiration);

        return new PreUploadFileCommandResponse
        {
            IsExist = false,
            Expiration = DateTimeOffset.UtcNow.Add(command.Expiration),
            FileId = fileEntity.Id,
            ObjectKey = fileEntity.ObjectKey,
            UploadUrl = new Uri(uploadUrl)
        };
    }

    /// <inheritdoc/>
    public async Task<FileUploadResult> UploadStreamAsync(UploadStreamFileCommand command, CancellationToken cancellationToken = default)
    {
        var fileEntity = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.ObjectKey == command.ObjectKey, cancellationToken);

        // 文件已上传完成，直接复用
        if (fileEntity != null && fileEntity.IsUploaded)
        {
            return new FileUploadResult
            {
                FileId = fileEntity.Id,
                ObjectKey = fileEntity.ObjectKey,
                FileSha256 = fileEntity.FileSha256,
                FileType = fileEntity.ContentType
            };
        }

        await _s3Client.UploadFileAsync(command.FileStream, command.ObjectKey, cancellationToken);

        if (fileEntity == null)
        {
            fileEntity = new FileEntity
            {
                FileExtension = Path.GetExtension(command.ObjectKey) ?? string.Empty,
                FileSha256 = command.SHA256,
                FileSize = command.FileSize,
                ContentType = command.ContentType,
                IsUploaded = true,
                ObjectKey = command.ObjectKey
            };

            await _databaseContext.Files.AddAsync(fileEntity, cancellationToken);
        }
        else
        {
            fileEntity.UpdateTime = DateTimeOffset.Now;
            fileEntity.IsUploaded = true;
            _databaseContext.Files.Update(fileEntity);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new FileUploadResult
        {
            FileId = fileEntity.Id,
            ObjectKey = fileEntity.ObjectKey,
            FileSha256 = fileEntity.FileSha256,
            FileType = fileEntity.ContentType
        };
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(long fileId, bool isSuccess, CancellationToken cancellationToken = default)
    {
        var fileEntity = await _databaseContext.Files.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        // 文件早已上传完毕，忽略用户请求
        if (fileEntity.IsUploaded)
        {
            return fileEntity.ObjectKey;
        }

        // 检查该文件是否当前用户上传的，否则无法完成上传
        if (fileEntity.UpdateUserId != _userContextProvider.GetUserContext().UserId)
        {
            // 记录已过期：长时间未完成上传或上传失败残留，废弃该记录，允许重新上传
            if (IsExpired(fileEntity))
            {
                await AbandonFileAsync(fileEntity, cancellationToken);
                throw new BusinessException("上传已过期，请重新上传") { StatusCode = 409 };
            }

            throw new BusinessException("其他用户正在上传此文件") { StatusCode = 409 };
        }

        var existFile = await _s3Client.FileExistsAsync(fileEntity.ObjectKey, cancellationToken);
        var fileLength = await _s3Client.GetFileSizeAsync(fileEntity.ObjectKey, cancellationToken);

        // 上传失败，清理残留文件
        if (!isSuccess)
        {
            if (existFile)
            {
                await _s3Client.DeleteFilesAsync(new[] { fileEntity.ObjectKey }, cancellationToken);
            }

            _databaseContext.Files.Remove(fileEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
            return fileEntity.ObjectKey;
        }

        // 上传已损坏：不存在或大小不一致
        if (!existFile || fileEntity.FileSize != fileLength)
        {
            if (existFile)
            {
                await _s3Client.DeleteFilesAsync(new[] { fileEntity.ObjectKey }, cancellationToken);
            }

            _databaseContext.Files.Remove(fileEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);

            throw new BusinessException("上传的文件已损坏") { StatusCode = 409 };
        }

        fileEntity.IsUploaded = true;
        _databaseContext.Update(fileEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return fileEntity.ObjectKey;
    }

    /// <inheritdoc/>
    public async Task DeleteFilesAsync(IReadOnlyCollection<long> fileIds, CancellationToken cancellationToken = default)
    {
        if (fileIds.Count == 0)
        {
            return;
        }

        var fileEntities = await _databaseContext.Files
            .Where(x => fileIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        _databaseContext.Files.RemoveRange(fileEntities);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var objectKeys = fileEntities.Select(x => x.ObjectKey).ToArray();
        await _s3Client.DeleteFilesAsync(objectKeys, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, Uri>> GetDownloadUrlsAsync(IReadOnlyCollection<KeyValueString> objectKeys, TimeSpan expiryDuration, CancellationToken cancellationToken = default)
    {
        var tasks = objectKeys.Select(async item =>
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                return KeyValuePair.Create<string, Uri?>(item.Key, null);
            }

            var url = await _s3Client.GeneratePreSignedDownloadUrlAsync(item.Key, expiryDuration);
            return KeyValuePair.Create<string, Uri?>(item.Key, url);
        });

        return GetUrlsCoreAsync(tasks, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Uri> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan expiryDuration, CancellationToken cancellationToken = default)
    {
        return await _s3Client.GeneratePreSignedDownloadUrlAsync(objectKey, expiryDuration);
    }

    /// <inheritdoc/>
    public async Task<bool> FileExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        return await _s3Client.FileExistsAsync(objectKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<StorageFileReadResult> ReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var fileEntity = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.ObjectKey == objectKey && x.IsUploaded, cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        var result = await _s3Client.ReadFileAsync(objectKey, cancellationToken);

        return new StorageFileReadResult
        {
            FileStream = result.FileStream,
            ContentType = fileEntity.ContentType,
            FileSize = fileEntity.FileSize,
            FileExtension = fileEntity.FileExtension,
            ObjectKey = fileEntity.ObjectKey
        };
    }

    /// <inheritdoc/>
    public Uri GetPublicFileUrl(string objectKey)
    {
        var server = _systemOptions.Server.TrimEnd('/');
        var key = objectKey.Trim('/');

        // 逐段转义，保留路径分隔符，避免 ObjectKey 中的 '/' 被转义为 %2F
        var escaped = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"{server}{StaticRoutePrefix}/{escaped}");
    }

    /// <inheritdoc/>
    public async Task<StorageFileInfo?> GetFileInfoAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var fileEntity = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.ObjectKey == objectKey, cancellationToken);

        if (fileEntity == null)
        {
            return null;
        }

        return new StorageFileInfo
        {
            FileId = fileEntity.Id,
            ObjectKey = fileEntity.ObjectKey,
            FileExtension = fileEntity.FileExtension,
            FileSha256 = fileEntity.FileSha256,
            FileSize = fileEntity.FileSize,
            ContentType = fileEntity.ContentType
        };
    }

    private static FileEntity CreateFileEntity(PreUploadFileCommand command)
    {
        return new FileEntity
        {
            FileExtension = Path.GetExtension(command.ObjectKey) ?? string.Empty,
            FileSha256 = command.SHA256,
            FileSize = command.FileSize,
            ContentType = command.ContentType,
            IsUploaded = false,
            ObjectKey = command.ObjectKey
        };
    }

    private bool IsExpired(FileEntity fileEntity)
    {
        return !fileEntity.IsUploaded && DateTimeOffset.Now - fileEntity.UpdateTime > PreUploadTimeout;
    }

    private async Task AbandonFileAsync(FileEntity fileEntity, CancellationToken cancellationToken)
    {
        if (await _s3Client.FileExistsAsync(fileEntity.ObjectKey, cancellationToken))
        {
            await _s3Client.DeleteFilesAsync(new[] { fileEntity.ObjectKey }, cancellationToken);
        }

        _databaseContext.Files.Remove(fileEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<string, Uri>> GetUrlsCoreAsync(IEnumerable<Task<KeyValuePair<string, Uri?>>> tasks, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(tasks);
        return results
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value!);
    }
}
