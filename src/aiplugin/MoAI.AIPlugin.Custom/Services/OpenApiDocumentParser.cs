using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

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
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream, cancellationToken);

        var functions = new List<OpenApiFunctionInfo>();
        foreach (var pathEntry in result.OpenApiDocument.Paths)
        {
            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var operation = operationEntry.Value;
                functions.Add(new OpenApiFunctionInfo
                {
                    Name = ResolveOperationName(operation.OperationId, operationEntry.Key, pathEntry.Key),
                    Summary = operation.Summary ?? operation.Description,
                    Path = pathEntry.Key,
                });
            }
        }

        var serverUrl = result.OpenApiDocument.Servers.FirstOrDefault()?.Url ?? string.Empty;

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
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream, cancellationToken);

        var operations = new List<OpenApiOperationInfo>();
        foreach (var pathEntry in result.OpenApiDocument.Paths)
        {
            foreach (var operationEntry in pathEntry.Value.Operations)
            {
                var operation = operationEntry.Value;
                operations.Add(new OpenApiOperationInfo
                {
                    Name = ResolveOperationName(operation.OperationId, operationEntry.Key, pathEntry.Key),
                    Summary = operation.Summary ?? operation.Description,
                    Path = pathEntry.Key,
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

    private static string ResolveOperationName(string? operationId, OperationType operationType, string path)
        => string.IsNullOrWhiteSpace(operationId) ? $"{operationType.ToString().ToUpperInvariant()} {path}" : operationId;
}

