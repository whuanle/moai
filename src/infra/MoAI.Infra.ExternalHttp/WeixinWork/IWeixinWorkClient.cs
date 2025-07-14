// <copyright file="IWeixinWorkClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using MoAI.Infra.WeixinWork.Models;
using Refit;

namespace MoAI.Infra.WeixinWork;

/// <summary>
/// 企业微信 API 客户端接口，参考 IOAuthClient 设计，提供获取用户身份的能力.
/// </summary>
public interface IWeixinWorkClient
{
    /// <summary>
    /// 根据 code 获取成员信息（企业成员或外部联系人）.
    /// </summary>
    /// <param name="accessToken">接口调用凭证</param>
    /// <param name="code">成员授权获取到的 code</param>
    /// <returns>获取用户身份的结果</returns>
    [Post("/cgi-bin/auth/getuserinfo")]
    Task<GetUserInfoResult> GetUserInfoAsync(string accessToken, string code);

    /// <summary>
    /// 获取访问用户敏感信息.
    /// </summary>
    /// <param name="accessToken">接口调用凭证</param>
    /// <param name="request">包含 user_ticket 的请求体</param>
    /// <returns>获取用户敏感信息的结果</returns>
    [Post("/cgi-bin/auth/getuserdetail")]
    Task<GetUserDetailResult> GetUserDetailAsync(string accessToken, [Body] GetUserDetailRequest request);

    /// <summary>
    /// 获取企业微信 access_token.
    /// </summary>
    /// <param name="corpid">企业ID</param>
    /// <param name="corpsecret">应用凭证密钥</param>
    /// <returns>access_token 结果</returns>
    [Get("/cgi-bin/gettoken")]
    Task<GetTokenResult> GetTokenAsync([AliasAs("corpid")] string corpid, [AliasAs("corpsecret")] string corpsecret);
}
