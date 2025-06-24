// <copyright file="QueryServerInfoResponse.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Public.Queries.Response;

/// <summary>
/// 服务器信息.
/// </summary>
public class QueryServerInfoCommandResponse
{
    /// <summary>
    /// 已经初始化.
    /// </summary>
    public bool IsInit { get; init; }

    /// <summary>
    /// 系统访问地址.
    /// </summary>
    public string ServiceUrl { get; init; } = default!;

    /// <summary>
    /// 公共存储地址，静态资源时可直接访问.
    /// </summary>
    public string PublicStoreUrl { get; init; } = default!;

    /// <summary>
    /// RSA 公钥，用于加密密码等信息传输到服务器.
    /// </summary>
    public string RsaPublic { get; init; } = default!;
}