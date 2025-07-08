// <copyright file="WikiConfig.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库配置.
/// </summary>
public class WikiConfig
{
    /// <summary>
    /// 指定进行文档向量化的模型.
    /// </summary>
    public int EmbeddingModelId { get; set; }

    /// <summary>
    /// 分词器.
    /// </summary>
    public string EmbeddingModelTokenizer { get; set; } = default!;

    /// <summary>
    /// 维度，跟模型有关，小于嵌入向量的最大值.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// 批处理大小.
    /// </summary>
    public int EmbeddingBatchSize { get; set; }

    /// <summary>
    /// 文档处理最大重试次数.
    /// </summary>
    public int MaxRetries { get; set; }
}