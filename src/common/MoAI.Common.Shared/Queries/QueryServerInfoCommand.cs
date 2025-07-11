// <copyright file="QueryServerInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Common.Queries.Response;

namespace MoAI.Common.Queries;

/// <summary>
/// 查询服务端公开配置信息.
/// </summary>
public class QueryServerInfoCommand : IRequest<QueryServerInfoCommandResponse>
{
}
