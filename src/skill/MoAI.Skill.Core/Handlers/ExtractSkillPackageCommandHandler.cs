using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Skill.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="ExtractSkillPackageCommand"/>
/// </summary>
public partial class ExtractSkillPackageCommandHandler : IRequestHandler<ExtractSkillPackageCommand, ExtractSkillPackageCommandResponse>
{
    /// <summary>
    /// 解压后条目数量上限.
    /// </summary>
    public const int MaxEntries = 100;

    /// <summary>
    /// 解压后总大小上限（50MB），防 zip 炸弹.
    /// </summary>
    public const int MaxTotalUncompressedSize = 50 * 1024 * 1024;

    private const string SkillMetaFileName = "SKILL.md";

    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractSkillPackageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public ExtractSkillPackageCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    [GeneratedRegex("^[a-zA-Z0-9_][a-zA-Z0-9_/.-]{0,199}$")]
    private static partial Regex EntryPathRegex();

    /// <inheritdoc/>
    public async Task<ExtractSkillPackageCommandResponse> Handle(ExtractSkillPackageCommand request, CancellationToken cancellationToken)
    {
        var zipFile = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (zipFile == null || !zipFile.IsUploaded)
        {
            throw new BusinessException("压缩包文件不存在或未完成上传.") { StatusCode = 404 };
        }

        if (!string.Equals(zipFile.FileExtension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("仅支持解压 zip 格式的技能包.") { StatusCode = 400 };
        }

        var storageFile = await _storageService.ReadAsync(zipFile.ObjectKey, cancellationToken);

        await using (storageFile.FileStream)
        using (var archive = new ZipArchive(storageFile.FileStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var entries = archive.Entries
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .ToList();

            if (entries.Count == 0)
            {
                throw new BusinessException("压缩包内没有文件.") { StatusCode = 400 };
            }

            if (entries.Count > MaxEntries)
            {
                throw new BusinessException($"压缩包内文件数量超过上限 {MaxEntries}.") { StatusCode = 400 };
            }

            var totalSize = (long)entries.Sum(x => x.Length);
            if (totalSize > MaxTotalUncompressedSize)
            {
                throw new BusinessException("压缩包解压后总大小超过 50MB 上限.") { StatusCode = 400 };
            }

            // GitHub 风格导出包常带一层目录：全部条目共享唯一顶层目录时剥掉该前缀，SKILL.md 落在根上
            var paths = entries.Select(x => NormalizeEntryPath(x.FullName)).ToList();
            var topDirs = paths
                .Where(p => p.Contains('/'))
                .Select(p => p[..p.IndexOf('/')])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var hasRootFile = paths.Any(p => !p.Contains('/'));
            var stripPrefix = topDirs.Count == 1 && !hasRootFile ? $"{topDirs[0]}/" : string.Empty;

            var files = new List<SkillFileItem>();
            string? skillMeta = null;

            foreach (var entry in entries)
            {
                var path = NormalizeEntryPath(entry.FullName);
                if (stripPrefix.Length > 0 && path.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    path = path[stripPrefix.Length..];
                }

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                ValidateEntryPath(path);

                var extension = Path.GetExtension(path);
                if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessException("压缩包内不允许嵌套 zip 文件.") { StatusCode = 400 };
                }

                using var buffer = new MemoryStream();
                await using (var stream = entry.Open())
                {
                    await stream.CopyToAsync(buffer, cancellationToken);
                }

                var bytes = buffer.ToArray();
                if (string.Equals(path, SkillMetaFileName, StringComparison.OrdinalIgnoreCase))
                {
                    skillMeta = Encoding.UTF8.GetString(bytes);
                }

                var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

                var objectKey = FileStoreHelper.GetObjectKey(sha256: sha256, fileName: path, prefix: "skill");
                var uploadResult = await _storageService.UploadStreamAsync(new UploadStreamFileCommand
                {
                    FileStream = new MemoryStream(bytes),
                    ContentType = extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ? "text/markdown" : "application/octet-stream",
                    FileSize = bytes.Length,
                    SHA256 = sha256,
                    ObjectKey = objectKey,
                }, cancellationToken);

                files.Add(new SkillFileItem
                {
                    Path = path,
                    FileId = uploadResult.FileId,
                    FileName = Path.GetFileName(path),
                });
            }

            var (name, description, instructions) = ParseSkillMeta(skillMeta);

            return new ExtractSkillPackageCommandResponse
            {
                Name = name,
                Description = description,
                Instructions = instructions,
                Files = files,
            };
        }
    }

    /// <summary>
    /// 规范化条目路径：统一分隔符为 /，去掉目录遍历段.
    /// </summary>
    /// <param name="fullName">zip 条目完整路径.</param>
    /// <returns>规范化后的路径.</returns>
    private static string NormalizeEntryPath(string fullName)
    {
        return fullName.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// 校验条目路径：与技能包文件路径规则一致（字母/数字/下划线/点/横线/斜杠，禁 .. 与绝对路径）.
    /// </summary>
    /// <param name="path">条目路径.</param>
    private static void ValidateEntryPath(string path)
    {
        if (!EntryPathRegex().IsMatch(path) || path.Contains("..") || path.StartsWith('/'))
        {
            throw new BusinessException($"压缩包内文件路径不合法: {path}") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 解析 SKILL.md：frontmatter（--- 包裹的 name:/description:）+ 之后的正文作为使用说明；无 frontmatter 时正文整体作为使用说明.
    /// </summary>
    /// <param name="skillMeta">SKILL.md 文本，可为 null.</param>
    /// <returns>（名称, 描述, 使用说明）三元组.</returns>
    private static (string Name, string Description, string Instructions) ParseSkillMeta(string? skillMeta)
    {
        if (string.IsNullOrWhiteSpace(skillMeta))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var text = skillMeta.TrimStart();
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return (string.Empty, string.Empty, skillMeta.Trim());
        }

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var rest = text[(firstLineEnd + 1)..];
        var fenceEnd = rest.IndexOf("\n---", StringComparison.Ordinal);
        if (fenceEnd < 0)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var frontmatter = rest[..fenceEnd];
        var instructions = rest[(fenceEnd + 4)..].Trim();

        var name = string.Empty;
        var description = string.Empty;
        foreach (var line in frontmatter.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');
            if (key.Equals("name", StringComparison.OrdinalIgnoreCase) && name.Length == 0)
            {
                name = value;
            }
            else if (key.Equals("description", StringComparison.OrdinalIgnoreCase) && description.Length == 0)
            {
                description = value;
            }
        }

        return (name, description, instructions);
    }
}
