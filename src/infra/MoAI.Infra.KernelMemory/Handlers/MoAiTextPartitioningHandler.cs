// Copyright (c) Microsoft. All rights reserved.
#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Chunkers;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Extensions;
using Microsoft.KernelMemory.Pipeline;

namespace Microsoft.KernelMemory.Handlers;

[InjectOnTransient]
public sealed class MoAiTextPartitioningHandler
{
    private readonly ILogger<TextPartitioningHandler> _log;
    private readonly PlainTextChunker _plainTextChunker;
    private readonly MarkDownChunker _markDownChunker;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoAiTextPartitioningHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">Application logger factory</param>
    public MoAiTextPartitioningHandler(ILoggerFactory loggerFactory)
    {
        this._plainTextChunker = new PlainTextChunker(new CL100KTokenizer());
        this._markDownChunker = new MarkDownChunker(new CL100KTokenizer());

        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TextPartitioningHandler>();
    }

    public async Task<List<string>> PartitionAsync(string text, FileContent fileContent, int maxTokensPerChunk, int overlappingTokens, string? chunkHeader, CancellationToken cancellationToken = default)
    {
        List<string> chunks;
        string mimeType = fileContent.MimeType ?? MimeTypes.PlainText;

        if (string.IsNullOrWhiteSpace(text))
        {
            this._log.LogWarning("The input text is empty. Skip the block.");
            return [];
        }

        switch (mimeType)
        {
            case MimeTypes.PlainText:
                this._log.LogDebug("The input text is empty. Skip the block. Block plain text content");
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
                this._log.LogDebug("Chunked Markdown content");
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
                this._log.LogWarning("Unsupported MIME type: {MimeType}", mimeType);
                return [];
        }

        this._log.LogDebug("Completed in chunks, there are a total of {0} chunks.", chunks.Count);
        return chunks;
    }
}
