// <copyright file="SetSystemSettingsCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Admin.SystemSettings.Commands;
using MoAI.Infra.Models;

namespace MoAI.Admin.SystemSettings.Queries;

public class SetSystemSettingsCommand : IRequest<EmptyCommandResponse>
{
    public IReadOnlyCollection<KeyValue<string, string>> Settings { get; init; } = new List<KeyValue<string, string>>();
}

public class QuerySystemSettingsCommand : IRequest<QuerySystemSettingsCommandResponse>
{
}
