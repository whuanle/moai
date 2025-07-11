// <copyright file="SetSystemSettingsCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Admin.SystemSettings.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Admin.SystemSettings.Queries;

/// <summary>
/// 查询系统设置.
/// </summary>
public class QuerySystemSettingsCommand : IRequest<QuerySystemSettingsCommandResponse>
{
}
