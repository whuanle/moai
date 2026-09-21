using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Feishu.Models;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书云文档客户端实现：知识空间节点遍历、docx 原始内容读取、云文档事件订阅与取消订阅.
/// </summary>
[InjectOnSingleton]
public sealed partial class FeishuDriveClient : IFeishuDriveClient
{
    /// <summary>
    /// 单次遍历子节点上限，防止超大知识库拖垮同步.
    /// </summary>
    private const int MaxChildNodes = 500;

    /// <summary>
    /// 子节点分页大小.
    /// </summary>
    private const int PageSize = 50;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FeishuAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuDriveClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="accessTokenProvider">租户访问凭证提供者.</param>
    public FeishuDriveClient(IHttpClientFactory httpClientFactory, FeishuAccessTokenProvider accessTokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _accessTokenProvider = accessTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<FeishuWikiNode?> GetWikiNodeAsync(Guid feishuAppId, string nodeToken, CancellationToken cancellationToken = default)
    {
        var credential = await _accessTokenProvider.GetCredentialAsync(feishuAppId, cancellationToken);
        var accessToken = await _accessTokenProvider.GetTenantAccessTokenAsync(feishuAppId, cancellationToken);

        var url = $"{credential.Domain}/open-apis/wiki/v2/spaces/get_node?token={Uri.EscapeDataString(nodeToken)}";
        var response = await SendAsync<WikiGetNodeResponse>(HttpMethod.Get, url, accessToken, null, cancellationToken);
        var node = response?.Data?.Node;
        if (node == null || string.IsNullOrWhiteSpace(node.NodeToken))
        {
            return null;
        }

        return ToNode(node, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeishuWikiNode>> ListChildNodesAsync(Guid feishuAppId, string spaceId, string? parentNodeToken, CancellationToken cancellationToken = default)
    {
        var credential = await _accessTokenProvider.GetCredentialAsync(feishuAppId, cancellationToken);
        var accessToken = await _accessTokenProvider.GetTenantAccessTokenAsync(feishuAppId, cancellationToken);

        var result = new List<FeishuWikiNode>();
        string? pageToken = null;

        do
        {
            var url = $"{credential.Domain}/open-apis/wiki/v2/spaces/{Uri.EscapeDataString(spaceId)}/nodes?page_size={PageSize}";
            if (!string.IsNullOrWhiteSpace(parentNodeToken))
            {
                url += $"&parent_node_token={Uri.EscapeDataString(parentNodeToken)}";
            }

            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                url += $"&page_token={Uri.EscapeDataString(pageToken)}";
            }

            var response = await SendAsync<WikiNodesResponse>(HttpMethod.Get, url, accessToken, null, cancellationToken);
            var data = response?.Data;
            if (data?.Items == null || data.Items.Count == 0)
            {
                break;
            }

            foreach (var item in data.Items)
            {
                if (string.IsNullOrWhiteSpace(item.NodeToken))
                {
                    continue;
                }

                result.Add(ToNode(item, parentNodeToken));
            }

            pageToken = data.HasMore == true && !string.IsNullOrWhiteSpace(data.PageToken) ? data.PageToken : null;
        }
        while (pageToken != null && result.Count < MaxChildNodes);

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> GetDocxRawContentAsync(Guid feishuAppId, string documentId, CancellationToken cancellationToken = default)
    {
        var credential = await _accessTokenProvider.GetCredentialAsync(feishuAppId, cancellationToken);
        var accessToken = await _accessTokenProvider.GetTenantAccessTokenAsync(feishuAppId, cancellationToken);

        var url = $"{credential.Domain}/open-apis/docx/v1/documents/{Uri.EscapeDataString(documentId)}/raw_content";
        var response = await SendAsync<RawContentResponse>(HttpMethod.Get, url, accessToken, null, cancellationToken);
        return response?.Data?.Content ?? string.Empty;
    }

    /// <inheritdoc/>
    public Task SubscribeDocEventAsync(Guid feishuAppId, string fileToken, string fileType = "docx", CancellationToken cancellationToken = default)
    {
        var url = $"/open-apis/drive/v1/files/{Uri.EscapeDataString(fileToken)}/subscribe?file_type={Uri.EscapeDataString(fileType)}";
        return SendWithTokenAsync(feishuAppId, HttpMethod.Post, url, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UnsubscribeDocEventAsync(Guid feishuAppId, string fileToken, CancellationToken cancellationToken = default)
    {
        var url = $"/open-apis/drive/v1/files/{Uri.EscapeDataString(fileToken)}/delete_subscribe";
        return SendWithTokenAsync(feishuAppId, HttpMethod.Delete, url, cancellationToken);
    }

    private async Task SendWithTokenAsync(Guid feishuAppId, HttpMethod method, string relativeUrl, CancellationToken cancellationToken)
    {
        var credential = await _accessTokenProvider.GetCredentialAsync(feishuAppId, cancellationToken);
        var accessToken = await _accessTokenProvider.GetTenantAccessTokenAsync(feishuAppId, cancellationToken);

        using var emptyBody = new StringContent("{}", Encoding.UTF8, "application/json");
        await SendAsync<EmptyResponse>(method, $"{credential.Domain}{relativeUrl}", accessToken, emptyBody, cancellationToken);
    }

    private async Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method,
        string url,
        string accessToken,
        HttpContent? content,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var httpClient = _httpClientFactory.CreateClient(FeishuApiClient.HttpClientName);
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (content != null)
        {
            request.Content = content;
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);

        var code = result == null ? -1 : GetCode(result);
        if (code != 0)
        {
            throw new BusinessException($"飞书云文档接口调用失败：{GetMsg(result) ?? "响应为空"}") { StatusCode = 400 };
        }

        return result;
    }

    private static int GetCode(object response)
    {
        var property = response.GetType().GetProperty("Code");
        return property?.GetValue(response) is int code ? code : -1;
    }

    private static string? GetMsg(object response)
    {
        var property = response.GetType().GetProperty("Msg");
        return property?.GetValue(response) as string;
    }

    private static FeishuWikiNode ToNode(WikiNodeDto node, string? fallbackParent)
    {
        return new FeishuWikiNode
        {
            NodeToken = node.NodeToken ?? string.Empty,
            ObjToken = node.ObjToken ?? string.Empty,
            ObjType = node.ObjType ?? string.Empty,
            Title = node.Title ?? string.Empty,
            SpaceId = node.SpaceId ?? string.Empty,
            HasChild = node.HasChild ?? false,
            ParentNodeToken = node.ParentNodeToken ?? fallbackParent,
            ObjEditTime = node.ObjEditTime,
        };
    }

    private sealed record WikiGetNodeResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("data")] WikiGetNodeData? Data);

    private sealed record WikiGetNodeData([property: JsonPropertyName("node")] WikiNodeDto? Node);

    private sealed record WikiNodesResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("data")] WikiNodesData? Data);

    private sealed record WikiNodesData(
        [property: JsonPropertyName("items")] List<WikiNodeDto>? Items,
        [property: JsonPropertyName("page_token")] string? PageToken,
        [property: JsonPropertyName("has_more")] bool? HasMore);

    private sealed record WikiNodeDto(
        [property: JsonPropertyName("node_token")] string? NodeToken,
        [property: JsonPropertyName("obj_token")] string? ObjToken,
        [property: JsonPropertyName("obj_type")] string? ObjType,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("space_id")] string? SpaceId,
        [property: JsonPropertyName("has_child")] bool? HasChild,
        [property: JsonPropertyName("parent_node_token")] string? ParentNodeToken,
        [property: JsonPropertyName("obj_edit_time")] string? ObjEditTime);

    private sealed record RawContentResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("data")] RawContentData? Data);

    private sealed record RawContentData([property: JsonPropertyName("content")] string? Content);

    private sealed record EmptyResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("msg")] string? Msg);
}
