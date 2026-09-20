using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// OpenApi 自定义插件运行时：解析已上传的 OpenAPI 文档并按其定义发起 HTTP 调用.
/// </summary>
public sealed class OpenApiToolCallService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiToolCallService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储服务.</param>
    public OpenApiToolCallService(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <summary>
    /// 读取并解析该 OpenAPI 插件对应的操作定义.
    /// </summary>
    /// <param name="custom">自定义插件.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>操作定义集合.</returns>
    public async Task<IReadOnlyList<OpenApiOperationInfo>> LoadOperationsAsync(PluginCustomEntity custom, CancellationToken cancellationToken)
    {
        if (custom.OpenapiFileId <= 0)
        {
            throw new BusinessException("OpenAPI 插件未上传文档.") { StatusCode = 400 };
        }

        var objectKey = await _databaseContext.Files
            .Where(x => x.Id == custom.OpenapiFileId)
            .Select(x => x.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new BusinessException("OpenAPI 文档文件不存在.") { StatusCode = 404 };
        }

        var file = await _storageService.ReadAsync(objectKey, cancellationToken);
        await using var stream = file.FileStream;
        return await OpenApiDocumentParser.ParseOperationsAsync(stream, cancellationToken);
    }

    /// <summary>
    /// 调用 OpenAPI 接口.
    /// </summary>
    /// <param name="custom">自定义插件.</param>
    /// <param name="operationName">操作名（OperationId）.</param>
    /// <param name="argumentsJson">参数 JSON 文本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应正文文本.</returns>
    public async Task<string> CallAsync(PluginCustomEntity custom, string operationName, string? argumentsJson, CancellationToken cancellationToken)
    {
        var operations = await LoadOperationsAsync(custom, cancellationToken);
        var operation = operations.FirstOrDefault(x => x.Name == operationName)
            ?? throw new BusinessException($"OpenAPI 接口不存在：{operationName}") { StatusCode = 404 };

        var args = ParseArguments(argumentsJson);
        var path = operation.Path;
        var queryParts = new List<string>();
        var headerParts = new List<KeyValueString>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in operation.Parameters)
        {
            if (!args.TryGetValue(parameter.Name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (parameter.Required)
                {
                    throw new BusinessException($"缺少必填参数：{parameter.Name}") { StatusCode = 400 };
                }

                continue;
            }

            var text = ToText(value);
            switch (parameter.In)
            {
                case "path":
                    path = path.Replace("{" + parameter.Name + "}", Uri.EscapeDataString(text), StringComparison.OrdinalIgnoreCase);
                    consumed.Add(parameter.Name);
                    break;
                case "header":
                    headerParts.Add(new KeyValueString { Key = parameter.Name, Value = text });
                    consumed.Add(parameter.Name);
                    break;
                case "query":
                    queryParts.Add($"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(text)}");
                    consumed.Add(parameter.Name);
                    break;
            }
        }

        var baseUrl = (custom.Server ?? string.Empty).TrimEnd('/');
        var queryString = BuildQueryString(custom, queryParts);
        var url = baseUrl + path + (queryString.Length > 0 ? "?" + queryString : string.Empty);

        using var request = new HttpRequestMessage(new HttpMethod(operation.Method), url);
        foreach (var header in DeserializeKeyValues(custom.Headers).Where(x => !x.Key.StartsWith('.')))
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in headerParts)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (operation.Method is "POST" or "PUT" or "PATCH")
        {
            var body = new Dictionary<string, object?>();
            foreach (var kv in args)
            {
                if (!consumed.Contains(kv.Key))
                {
                    body[kv.Key] = kv.Value;
                }
            }

            if (body.Count > 0)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            throw new BusinessException($"访问 OpenAPI 接口失败：{ex.Message}") { StatusCode = 400 };
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new BusinessException($"OpenAPI 接口返回 {(int)response.StatusCode}：{Truncate(text)}") { StatusCode = 400 };
            }

            return string.IsNullOrWhiteSpace(text) ? "{\"success\":true}" : text;
        }
    }

    /// <summary>
    /// 合并插件自定义 Query 与文档参数生成的 query 到请求 URL 查询串（自定义 Query 先，文档参数后，均 URL 编码）.
    /// </summary>
    private static string BuildQueryString(PluginCustomEntity custom, List<string> queryParts)
    {
        var customQueries = DeserializeKeyValues(custom.Queries);
        if (customQueries.Count == 0)
        {
            return string.Join("&", queryParts);
        }

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        foreach (var kv in customQueries)
        {
            query[kv.Key] = kv.Value;
        }

        var customPart = query.ToString() ?? string.Empty;
        return queryParts.Count > 0 ? customPart + "&" + string.Join("&", queryParts) : customPart;
    }

    private static Dictionary<string, JsonElement> ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson) || argumentsJson.Trim() == "{}")
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson, JsonOptions)
                ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            throw new BusinessException("工具参数 JSON 解析失败.") { StatusCode = 400 };
        }
    }

    private static string ToText(JsonElement element)
        => element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();

    private static List<KeyValueString> DeserializeKeyValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<KeyValueString>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
