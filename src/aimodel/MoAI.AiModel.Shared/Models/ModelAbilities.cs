// <copyright file="ModelAbilities.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.AiModel.Models;

/// <summary>
/// ModelAbilities.
/// </summary>
public class ModelAbilities
{
    /// <summary>
    /// Whether model supports file upload
    /// </summary>
    public bool? Files { get; init; } = default!;

    /// <summary>
    /// Whether model supports function call
    /// </summary>
    public bool? FunctionCall { get; init; } = default!;

    /// <summary>
    /// Whether model supports image output
    /// </summary>
    public bool? ImageOutput { get; init; } = default!;

    /// <summary>
    /// Whether model supports vision
    /// </summary>
    public bool? Vision { get; init; } = default!;
}
