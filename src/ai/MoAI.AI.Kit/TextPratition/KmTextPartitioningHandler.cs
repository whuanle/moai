// Copyright (c) Microsoft. All rights reserved.
#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Chunkers;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;

namespace Microsoft.KernelMemory.Handlers;

/// <summary>
/// 文档切割.
/// </summary>
public sealed class KmTextPartitioningHandler
{
    private readonly PlainTextChunker _plainTextChunker;
    private readonly MarkDownChunker _markDownChunker;

    /// <summary>
    /// Initializes a new instance of the <see cref="KmTextPartitioningHandler"/> class.
    /// </summary>
    /// <param name="textTokenizer"></param>
    public KmTextPartitioningHandler(ITextTokenizer? textTokenizer = null)
    {
        if (textTokenizer == null)
        {
            textTokenizer = new CL100KTokenizer();
        }

        // Microsoft.ML.Tokenizers. TiktokenTokenizer.CreateForModelAsync
        this._plainTextChunker = new PlainTextChunker(textTokenizer);
        this._markDownChunker = new MarkDownChunker(textTokenizer);
    }

    /// <summary>
    /// 切割.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="mimeType"></param>
    /// <param name="maxTokensPerChunk"></param>
    /// <param name="overlappingTokens"></param>
    /// <param name="chunkHeader"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<string>> PartitionAsync(string text, string mimeType, int maxTokensPerChunk, int overlappingTokens, string? chunkHeader, CancellationToken cancellationToken = default)
    {
        List<string> chunks;

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        switch (mimeType)
        {
            case MimeTypes.PlainText:
                chunks = this._plainTextChunker.Split(
                    text,
                    new PlainTextChunkerOptions
                    {
                        MaxTokensPerChunk = maxTokensPerChunk,
                        Overlap = overlappingTokens,
                        ChunkHeader = chunkHeader
                    });
                break;

            case MimeTypes.MarkDown:
                chunks = this._markDownChunker.Split(
                    text,
                    new MarkDownChunkerOptions
                    {
                        MaxTokensPerChunk = maxTokensPerChunk,
                        Overlap = overlappingTokens,
                        ChunkHeader = chunkHeader
                    });
                break;

            default:
                return [];
        }

        return chunks;
    }
}
