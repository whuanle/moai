using Microsoft.OpenApi.Readers;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// OpenApi 文档解析器，读取文件流并拆解每个接口.
/// </summary>
internal static class OpenApiDocumentParser
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
            // 接口名称
            var operation = pathEntry.Value.Operations.First().Value;
            functions.Add(new OpenApiFunctionInfo
            {
                Name = operation.OperationId,
                Summary = operation.Summary,
                Path = pathEntry.Key,
            });
        }

        var serverUrl = result.OpenApiDocument.Servers.FirstOrDefault()?.Url ?? string.Empty;

        return new OpenApiParseResult
        {
            Server = serverUrl,
            Functions = functions,
        };
    }
}
