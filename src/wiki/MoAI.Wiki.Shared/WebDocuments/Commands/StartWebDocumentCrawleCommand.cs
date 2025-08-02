﻿// <copyright file="StartWebDocumentCrawleCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// 开始启动知识库 web 爬取任务.
/// </summary>
public class StartWebDocumentCrawleCommand : IRequest<SimpleGuid>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 web 配置 id.
    /// </summary>
    public int WebConfigId { get; init; }

    /// <summary>
    /// 文本分割方法，暂时不支持使用.
    /// </summary>
    public string SplitMethod { get; set; } = default!;

    /// <summary>
    /// 每个段落最大 token 数量.
    /// </summary>
    public int MaxTokensPerParagraph { get; set; } = 1000;

    /// <summary>
    /// 块之间重叠令牌的数量.
    /// </summary>
    public int OverlappingTokens { get; set; } = 100;

    /// <summary>
    /// 统计 tokens 数量的算法 支持: "p50k", "cl100k", "o200k".
    /// </summary>
    public EmbeddingTokenizer Tokenizer { get; set; } = EmbeddingTokenizer.P50k;
}
