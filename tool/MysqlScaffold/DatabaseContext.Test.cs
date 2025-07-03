// <copyright file="DatabaseContext.Test.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MoAI.Database;

public partial class DatabaseContext
{
    protected static void AuditFilter(EntityEntryEventArgs args)
    {
    }
}