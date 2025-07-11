// <copyright file="SetSystemSettingsCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Admin.SystemSettings.Commands;

/// <summary>
/// <inheritdoc cref="SetSystemSettingsCommand"/>
/// </summary>
public class SetSystemSettingsCommandHandler : IRequestHandler<SetSystemSettingsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSystemSettingsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public SetSystemSettingsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SetSystemSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _databaseContext.Settings.ToArrayAsync();

        foreach (var item in settings)
        {
            var setting = request.Settings.FirstOrDefault(x => x.Key == item.Key);
            if (setting != null)
            {
                item.Value = setting.Value;
            }
        }

        _databaseContext.UpdateRange(settings);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
