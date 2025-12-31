using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Infra.Put;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using System.Text;
using System.Text.Json;

namespace MoAI.Wiki.Plugins.OpenApi;

/// <summary>
/// <inheritdoc cref="ImportOpenApiToWikiCommand"/>
/// </summary>
public class ImportOpenApiToWikiCommandHandler : IRequestHandler<ImportOpenApiToWikiCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly IPutClient _putClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiToWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="putClient"></param>
    public ImportOpenApiToWikiCommandHandler(DatabaseContext databaseContext, IMediator mediator, IPutClient putClient)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _putClient = putClient;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ImportOpenApiToWikiCommand request, CancellationToken cancellationToken)
    {
        if (request.FileId != null && request.FileId > 0)
        {
            _ = await _mediator.Send(new ComplateFileUploadCommand
            {
                FileId = request.FileId.Value,
                IsSuccess = true,
            });

            var downResult = await _mediator.Send(
                new DownloadFileCommand
                {
                    FileId = request.FileId.Value
                },
                cancellationToken);

            var filePath = downResult.LocalFilePath;
            using FileStream fileStream = new FileStream(filePath, FileMode.Open);

            await UploadApiFileAsync(request, fileStream);

            await _mediator.Send(new DeleteFileCommand
            {
                FileIds = new[] { request.FileId.Value }
            });
        }
        else if (!string.IsNullOrWhiteSpace(request.OpenApiSpecUrl))
        {
            var filePath = Path.GetTempFileName();
            using var fileStream = File.Create(filePath);
            _putClient.Client.BaseAddress = new Uri(request.OpenApiSpecUrl);
            using var httpStream = await _putClient.DownloadAsync(string.Empty);
            await httpStream.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync();
            fileStream.Seek(0, SeekOrigin.Begin);
            await UploadApiFileAsync(request, fileStream);
        }

        return EmptyCommandResponse.Default;
    }

    private async Task UploadApiFileAsync(ImportOpenApiToWikiCommand request, FileStream fileStream)
    {
        await foreach (var item in ReadApiAsync(fileStream))
        {
            var md5 = FileStoreHelper.CalculateFileMd5(item);
            var fileName = Path.GetFileName(item);

            var objectKey = FileStoreHelper.GetObjectKey(md5: md5, fileName: fileName, prefix: $"wiki/{request.WikiId}");

            // 同一个知识库下不能有同key文件.
            var existDocument = await _databaseContext.WikiDocuments.Where(x => x.ObjectKey == objectKey).AnyAsync();

            if (existDocument)
            {
                continue;
            }

            using var apiFileStream = File.OpenRead(item);

            var result = await _mediator.Send(new UploadFileStreamCommand
            {
                ObjectKey = objectKey,
                MD5 = md5,
                ContentType = FileStoreHelper.GetMimeType(fileName),
                FileStream = apiFileStream,
                FileSize = (int)apiFileStream.Length
            });

            await _databaseContext.WikiDocuments.AddAsync(new Database.Entities.WikiDocumentEntity
            {
                FileId = result.FileId,
                WikiId = request.WikiId,
                FileName = fileName,
                ObjectKey = objectKey,
                FileType = Path.GetExtension(fileName)
            });

            await _databaseContext.SaveChangesAsync();
        }
    }

    // 流式返回接口文档路径
    private static async IAsyncEnumerable<string> ReadApiAsync(Stream fileStream)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(tempDir);

        var reader = new OpenApiStreamReader();
        var apiReaderResult = await reader.ReadAsync(fileStream);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var sb = new StringBuilder();

        foreach (var pathEntry in apiReaderResult.OpenApiDocument.Paths)
        {
            sb.Clear();

            var path = pathEntry.Key;
            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var method = operationEntry.Key.ToString().ToUpper();
                var operation = operationEntry.Value;
                var operationId = operation.OperationId;

                // 接口名称和描述
                sb.AppendLine($"# {operation.Summary ?? "未命名接口"}");
                sb.AppendLine();
                sb.AppendLine($"**接口描述:** {operation.Description ?? "无"}");
                sb.AppendLine();

                // 接口地址和请求方式
                sb.AppendLine("## 接口详情");
                sb.AppendLine($"- **接口地址:** `{path}`");
                sb.AppendLine($"- **请求方式:** `{method}`");
                sb.AppendLine();

                // 请求参数
                sb.AppendLine("## 请求参数");

                var queryParameters = operation.Parameters.Where(p => p.In == ParameterLocation.Query).ToList()!;
                OpenApiMediaType? requestBodyContent = default!;
                bool hasRequestBody = false;
                if (operation.RequestBody != null && operation.RequestBody.Content.TryGetValue("application/json", out requestBodyContent) && requestBodyContent != null)
                {
                    hasRequestBody = true;
                }
                else
                {
                    hasRequestBody = false;
                }

                if (queryParameters.Count == 0 && !hasRequestBody)
                {
                    sb.AppendLine("无请求参数。");
                }
                else
                {
                    if (queryParameters.Count != 0)
                    {
                        sb.AppendLine("### Query 参数");
                        sb.AppendLine();
                        sb.AppendLine("| 名称 | 类型 | 是否必需 | 描述 |");
                        sb.AppendLine("| --- | --- | --- | --- |");
                        foreach (var p in queryParameters)
                        {
                            var schema = p.Schema;
                            var typeInfo = $"{schema.Type}{(schema.Format != null ? $"({schema.Format})" : string.Empty)}";
                            sb.AppendLine($"| `{p.Name}` | `{typeInfo}` | {(p.Required ? "是" : "否")} | {p.Description?.Replace("\n", "<br/>", StringComparison.CurrentCultureIgnoreCase) ?? "无"} |");
                        }

                        sb.AppendLine();
                    }

                    if (hasRequestBody)
                    {
                        sb.AppendLine("### 请求体");
                        sb.AppendLine();
                        var schema = requestBodyContent!.Schema;
                        sb.AppendLine("```json");
                        sb.AppendLine(JsonSerializer.Serialize(SchemaToJson(schema, apiReaderResult.OpenApiDocument.Components.Schemas), jsonOptions));
                        sb.AppendLine("```");
                    }
                }

                sb.AppendLine();

                // 响应结果
                sb.AppendLine("## 响应结果");
                if (operation.Responses.TryGetValue("200", out var response) && response.Content.TryGetValue("application/json", out var responseBodyContent))
                {
                    var schema = responseBodyContent.Schema;
                    sb.AppendLine("```json");
                    sb.AppendLine(JsonSerializer.Serialize(SchemaToJson(schema, apiReaderResult.OpenApiDocument.Components.Schemas), jsonOptions));
                    sb.AppendLine("```");
                }
                else
                {
                    sb.AppendLine("无 '200 OK' 响应示例。");
                }

                // 写入文件
                var fileName = SanitizeFileName(operationId ?? path.Replace('/', '_'));
                await File.WriteAllTextAsync(Path.Combine(tempDir, $"{fileName}.md"), sb.ToString());
                yield return Path.Combine(tempDir, $"{fileName}.md");
            }
        }
    }

    // 将 OpenApiSchema 转换为可序列化的对象
    private static object SchemaToJson(OpenApiSchema schema, IDictionary<string, OpenApiSchema> components)
    {
        if (!string.IsNullOrEmpty(schema.Reference?.Id))
        {
            if (components.TryGetValue(schema.Reference.Id, out var componentSchema))
            {
                schema = componentSchema;
            }
        }

        if (schema.AllOf.Any())
        {
            var allOfObj = new Dictionary<string, object>();
            foreach (var subSchema in schema.AllOf)
            {
                var subJson = SchemaToJson(subSchema, components);
                if (subJson is Dictionary<string, object> subDict)
                {
                    foreach (var kvp in subDict)
                    {
                        allOfObj[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 合并直接定义的属性
            foreach (var property in schema.Properties)
            {
                allOfObj[property.Key] = SchemaToJson(property.Value, components);
            }

            return allOfObj;
        }

        if (schema.Type == "object")
        {
            var obj = new Dictionary<string, object>();
            foreach (var property in schema.Properties)
            {
                obj[property.Key] = SchemaToJson(property.Value, components);
            }

            return obj;
        }

        if (schema.Type == "array")
        {
            // 如果 Items 为 null，则返回一个空数组或表示任意类型的数组
            if (schema.Items == null)
            {
                return new[] { "any" };
            }
            return new[] { SchemaToJson(schema.Items, components) };
        }

        // 为字段类型附加描述作为注释
        var typeInfo = $"{schema.Type}{(schema.Format != null ? $"({schema.Format})" : string.Empty)}";
        if (!string.IsNullOrWhiteSpace(schema.Description))
        {
            return $"{typeInfo} // {schema.Description}";
        }

        return typeInfo;
    }

    // 清理文件名，移除无效字符
    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
}
