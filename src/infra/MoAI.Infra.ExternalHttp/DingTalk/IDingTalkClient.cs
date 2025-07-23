// <copyright file="IDingTalkClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using MoAI.Infra.DingTalk.Models;
using Refit;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.DingTalk;

public interface IDingTalkClient
{
    /// <summary>
    /// 获取用户token.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/v1.0/oauth2/userAccessToken")]
    Task<UserAccessTokenResponse> GetUserAccessTokenAsync([Body] UserAccessTokenRequest request);

    /// <summary>
    /// 获取用户信息.
    /// </summary>
    /// <param name="unionId"></param>
    /// <param name="accessToken"></param>
    /// <returns></returns>
    [Get("/v1.0/contact/users/{unionId}")]
    Task<ContactUserInfoResponse> GetContactUserInfoAsync(
        [AliasAs("unionId")] string unionId,
        [Header("x-acs-dingtalk-access-token")] string accessToken);

    [Post("/sns/getuserinfo_bycode")]
    Task<SnsGetUserInfoByCodeResponse> GetUserInfoByCodeAsync(
    [Query] string accessKey,
    [Query] string timestamp,
    [Query] string signature,
    [Body] SnsGetUserInfoByCodeRequest request);

    /// <summary>
    /// 计算钉钉签名（HMAC-SHA256 + Base64 + UrlEncode）
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="appSecret">应用密钥</param>
    /// <returns>UrlEncode后的签名</returns>
    public string ComputeSignature(string timestamp, string appSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp));
        var base64 = Convert.ToBase64String(hash);
        return WebUtility.UrlEncode(base64);
    }
}
