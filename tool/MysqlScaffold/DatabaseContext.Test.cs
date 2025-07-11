// <copyright file="DatabaseContext.Test.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MoAI.Database;

/// <summary>
/// DatabaseContext.
/// </summary>
public partial class DatabaseContext
{
    /// <summary>
    /// 审计属性.
    /// </summary>
    /// <param name="args"></param>
    protected static void AuditFilter(EntityEntryEventArgs args)
    {
    }
}