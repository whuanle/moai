// <copyright file="SetEmbeddingGenerationDocumentTaskCommandHandler.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

namespace MaomiAI.Document.Core.Consumers.Events;

/// <summary>
/// 发送消息，后台执行任务.
/// </summary>
public class EmbeddingDocumentEvent
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public int TaskId { get; init; }
}
