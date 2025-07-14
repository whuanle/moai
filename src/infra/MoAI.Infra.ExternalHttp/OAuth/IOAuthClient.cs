// <copyright file="IOAuthClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using MoAI.Infra.OAuth.Models;
using Refit;

namespace MoAI.Infra.OAuth;

public interface IOAuthClient
{
    public HttpClient Client { get; }

    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdConfiguration> GetWellKnownAsync(string path);
}
