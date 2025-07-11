// <copyright file="RefreshAdminsCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 触发更新超级管理员列表事件.
/// </summary>
public class RefreshAdminsCommand : IRequest<EmptyCommandResponse>
{
}
