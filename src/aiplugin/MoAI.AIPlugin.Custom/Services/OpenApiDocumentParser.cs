using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// OpenApi 文档解析器，读取文件流并拆解每个接口.
/// </summary>
public static class OpenApiDocumentParser
{
    /// <summary>
    /// 解析 openapi 文档流，生成插件函数实体集合.
    /// </summary>
    /// <param name="stream">文件流.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>解析结果.</returns>
    public static async Task<OpenApiParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(stream, cancellationToken);

        var functions = new List<OpenApiFunctionInfo>();
        foreach (var pathEntry in document.Paths ?? [])
        {
            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var operation = operationEntry.Value;
                functions.Add(new OpenApiFunctionInfo
                {
                    Name = ResolveOperationName(operation.OperationId, operationEntry.Key.ToString().ToUpperInvariant(), pathEntry.Key),
                    Summary = operation.Summary ?? operation.Description,
                    Path = pathEntry.Key ?? string.Empty,
                });
            }
        }

        var serverUrl = document.Servers?.FirstOrDefault()?.Url ?? string.Empty;

        return new OpenApiParseResult
        {
            Server = serverUrl,
            Functions = functions,
        };
    }

    /// <summary>
    /// 解析 openapi 文档流，生成含 HTTP 方法与参数的完整操作信息（供运行时调用）.
    /// </summary>
    /// <param name="stream">文件流.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>操作信息集合.</returns>
    public static async Task<IReadOnlyList<OpenApiOperationInfo>> ParseOperationsAsync(Stream stream, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(stream, cancellationToken);

        var operations = new List<OpenApiOperationInfo>();
        foreach (var pathEntry in document.Paths ?? [])
        {
            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var operation = operationEntry.Value;
                operations.Add(new OpenApiOperationInfo
                {
                    Name = ResolveOperationName(operation.OperationId, operationEntry.Key.ToString().ToUpperInvariant(), pathEntry.Key),
                    Summary = operation.Summary ?? operation.Description,
                    Path = pathEntry.Key ?? string.Empty,
                    Method = operationEntry.Key.ToString().ToUpperInvariant(),
                    Parameters = operation.Parameters?.Select(p => new OpenApiParameterInfo
                    {
                        Name = p.Name,
                        In = p.In?.ToString()?.ToLowerInvariant() ?? "query",
                        Required = p.Required,
                        Description = p.Description,
                    }).ToList() ?? [],
                });
            }
        }

        return operations;
    }

    private static async Task<OpenApiDocument> LoadDocumentAsync(Stream stream, CancellationToken cancellationToken)
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();

        var result = await OpenApiDocument.LoadAsync(stream, format: null, settings: settings, cancellationToken);
        if (result.Document == null)
        {
            var errors = string.Join("; ", result.Diagnostic?.Errors ?? []);
            throw new InvalidOperationException($"OpenApi document parse failed. {errors}");
        }

        return result.Document;
    }

    private static string ResolveOperationName(string? operationId, string method, string path)
        => string.IsNullOrWhiteSpace(operationId) ? $"{method} {path}" : operationId;
}
