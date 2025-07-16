// <copyright file="SetSystemSettingsCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Models;
using MoAI.Infra.Exceptions;
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
        var defaultSetting = ISystemSettingProvider.Keys.FirstOrDefault(x => x.Key == request.Settings.Key);
        if (defaultSetting == default(SystemSettingKey))
        {
            throw new BusinessException("系统无此配置项");
        }

        var setting = await _databaseContext.Settings.FirstOrDefaultAsync(x => x.Key == request.Settings.Key);

        if (setting == null)
        {
            await _databaseContext.Settings.AddAsync(new Database.Entities.SettingEntity
            {
                Key = defaultSetting.Key,
                Value = request.Settings.Value,
                Description = defaultSetting.Description,
            });

            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _databaseContext.Update(setting);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
