// <copyright file="IFullAudited.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Database.Audits;

/// <summary>
/// 全部审计属性.
/// </summary>
public interface IFullAudited : ICreationAudited, IDeleteAudited
{
}
