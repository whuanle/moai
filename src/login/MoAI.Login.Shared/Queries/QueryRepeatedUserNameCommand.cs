// <copyright file="QueryRepeatedUserNameCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

/// <summary>
/// 检查用户名是否重复.
/// </summary>
public class QueryRepeatedUserNameCommand : IRequest<SimpleBool>
{
    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
}