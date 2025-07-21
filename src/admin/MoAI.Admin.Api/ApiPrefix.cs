// <copyright file="ApiPrefix.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>
#pragma warning disable SA1600 // Elements should be documented

namespace MoAI.Admin;

/// <summary>
/// Api 前缀.
/// </summary>
internal static class ApiPrefix
{
    public const string OAuth = "/admin/oauth";
    public const string Settings = "/admin/settings";
    public const string User = "/admin/user";
    public const string AiModel = "/admin/aimodel";
}
