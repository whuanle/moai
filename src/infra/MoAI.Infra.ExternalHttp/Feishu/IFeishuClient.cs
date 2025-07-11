// <copyright file="IFeishuClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Feishu.Models;
using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu;

public interface IFeishuClient
{
    public HttpClient Client { get; }

    [Post("/open-apis/authen/v2/oauth/token")]
    Task<FeishuTokenResponse> GetUserAccessTokenAsync(
        [Body(BodySerializationMethod.Serialized)] FeishuTokenRequest request);

    [Get("/open-apis/authen/v1/user_info")]
    Task<FeishuApiResponse<FeishuUserInfo>> UserInfo([Header("Authorization")] string authorization);
}

public class FeishuApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public required string Msg { get; set; }

    [JsonPropertyName("data")]
    public required T Data { get; set; }
}

public class FeishuUserInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("en_name")]
    public required string EnName { get; set; }

    [JsonPropertyName("avatar_url")]
    public required string AvatarUrl { get; set; }

    [JsonPropertyName("avatar_thumb")]
    public required string AvatarThumb { get; set; }

    [JsonPropertyName("avatar_middle")]
    public required string AvatarMiddle { get; set; }

    [JsonPropertyName("avatar_big")]
    public required string AvatarBig { get; set; }

    [JsonPropertyName("open_id")]
    public required string OpenId { get; set; }

    [JsonPropertyName("union_id")]
    public required string UnionId { get; set; }

    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("enterprise_email")]
    public required string EnterpriseEmail { get; set; }

    [JsonPropertyName("user_id")]
    public required string UserId { get; set; }

    [JsonPropertyName("mobile")]
    public required string Mobile { get; set; }

    [JsonPropertyName("tenant_key")]
    public required string TenantKey { get; set; }

    [JsonPropertyName("employee_no")]
    public required string EmployeeNo { get; set; }
}