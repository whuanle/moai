// <copyright file="ClearWikiDocumentEmbeddingCommand.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 清空知识库文档向量.
/// </summary>
public class ClearWikiDocumentEmbeddingCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 不填写时清空整个知识库的文档向量.
    /// </summary>
    public int? DocumentId { get; init; }
}
