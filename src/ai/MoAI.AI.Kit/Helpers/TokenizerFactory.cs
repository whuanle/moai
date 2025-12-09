#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using MoAI.AI.Models;

namespace MoAI.AI.Helpers;

public static class TokenizerFactory
{
    private static readonly (string Prefix, ModelEncoding Encoding)[] _modelPrefixToEncoding =
    {
        // chat
        ("o1-", ModelEncoding.O200kBase),       // e.g. o1-mini
        ("o3-", ModelEncoding.O200kBase),       // e.g. o3-mini
        ("gpt-4o-", ModelEncoding.O200kBase),   // e.g., gpt-4o-2024-05-13
        ("gpt-4-", ModelEncoding.Cl100kBase),   // e.g., gpt-4-0314, etc., plus gpt-4-32k
        ("gpt-3.5-", ModelEncoding.Cl100kBase), // e.g, gpt-3.5-turbo-0301, -0401, etc.
        ("gpt-35-", ModelEncoding.Cl100kBase)   // Azure deployment name
    };

    private static readonly Dictionary<string, ModelEncoding> _modelToEncoding =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // chat
        { "gpt-4o", ModelEncoding.O200kBase },
        { "o1", ModelEncoding.O200kBase },
        { "o3", ModelEncoding.O200kBase },
        { "gpt-4", ModelEncoding.Cl100kBase },
        { "gpt-3.5-turbo", ModelEncoding.Cl100kBase },
        { "gpt-3.5-turbo-16k", ModelEncoding.Cl100kBase },
        { "gpt-35", ModelEncoding.Cl100kBase },           // Azure deployment name
        { "gpt-35-turbo", ModelEncoding.Cl100kBase },     // Azure deployment name
        { "gpt-35-turbo-16k", ModelEncoding.Cl100kBase }, // Azure deployment name

        // text
        { "text-davinci-003", ModelEncoding.P50kBase },
        { "text-davinci-002", ModelEncoding.P50kBase },
        { "text-davinci-001", ModelEncoding.R50kBase },
        { "text-curie-001", ModelEncoding.R50kBase },
        { "text-babbage-001", ModelEncoding.R50kBase },
        { "text-ada-001", ModelEncoding.R50kBase },
        { "davinci", ModelEncoding.R50kBase },
        { "curie", ModelEncoding.R50kBase },
        { "babbage", ModelEncoding.R50kBase },
        { "ada", ModelEncoding.R50kBase },

        // code
        { "code-davinci-002", ModelEncoding.P50kBase },
        { "code-davinci-001", ModelEncoding.P50kBase },
        { "code-cushman-002", ModelEncoding.P50kBase },
        { "code-cushman-001", ModelEncoding.P50kBase },
        { "davinci-codex", ModelEncoding.P50kBase },
        { "cushman-codex", ModelEncoding.P50kBase },

        // edit
        { "text-davinci-edit-001", ModelEncoding.P50kEdit },
        { "code-davinci-edit-001", ModelEncoding.P50kEdit },

        // embeddings
        { "text-embedding-ada-002", ModelEncoding.Cl100kBase },
        { "text-embedding-3-small", ModelEncoding.Cl100kBase },
        { "text-embedding-3-large", ModelEncoding.Cl100kBase },

        // old embeddings
        { "text-similarity-davinci-001", ModelEncoding.R50kBase },
        { "text-similarity-curie-001", ModelEncoding.R50kBase },
        { "text-similarity-babbage-001", ModelEncoding.R50kBase },
        { "text-similarity-ada-001", ModelEncoding.R50kBase },
        { "text-search-davinci-doc-001", ModelEncoding.R50kBase },
        { "text-search-curie-doc-001", ModelEncoding.R50kBase },
        { "text-search-babbage-doc-001", ModelEncoding.R50kBase },
        { "text-search-ada-doc-001", ModelEncoding.R50kBase },
        { "code-search-babbage-code-001", ModelEncoding.R50kBase },
        { "code-search-ada-code-001", ModelEncoding.R50kBase },

        // open source
        { "gpt2", ModelEncoding.GPT2 }
    };

    public static ITextTokenizer? GetTokenizerForEncoding(string encodingId)
    {
        if (string.IsNullOrWhiteSpace(encodingId))
            return null;

        encodingId = encodingId.Trim().ToLowerInvariant();

        // Accept common canonical names and variants.
        if (encodingId.Contains("p50k", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.P50kBase);

        if (encodingId.Contains("p50k_edit", StringComparison.Ordinal) || encodingId.Contains("p50k-edit", StringComparison.Ordinal) || encodingId.Contains("p50kedit", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.P50kEdit);

        if (encodingId.Contains("cl100k", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.Cl100kBase);

        if (encodingId.Contains("o200k", StringComparison.Ordinal) || encodingId.Contains("o200", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.O200kBase);

        if (encodingId.Contains("r50k", StringComparison.Ordinal) || encodingId.Contains("r50", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.R50kBase);

        if (encodingId.Contains("gpt2", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.GPT2);

        // Fallback: try exact enum name match
        if (Enum.TryParse<ModelEncoding>(encodingId, true, out var parsed))
            return CreateTokenizerForEncoding(parsed);

        return null;
    }

    public static ITextTokenizer? GetTokenizerForModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return null;

        // Try TiktokenTokenizer first (preferred, may throw if model unknown)
        try
        {
            return new TiktokenTokenizer(modelId);
        }
        catch (KernelMemoryException)
        {
            // ignore and fallback to mapping heuristics
        }

        modelId = modelId.Trim();

        // Exact model mapping
        if (_modelToEncoding.TryGetValue(modelId, out var encoded))
            return CreateTokenizerForEncoding(encoded);

        // Prefix based mapping (for versions / date-suffixed model names)
        foreach (var (Prefix, Encoding) in _modelPrefixToEncoding)
        {
            if (modelId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return CreateTokenizerForEncoding(Encoding);
        }

        // Heuristic fallbacks to cover common naming patterns
        var lower = modelId.ToLowerInvariant();
        if (lower.StartsWith("text-embedding-", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.Cl100kBase);

        if (lower.StartsWith("gpt-3.5-", StringComparison.Ordinal) || lower.StartsWith("gpt-35-", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.Cl100kBase);

        if (lower.StartsWith("gpt-4o-", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.O200kBase);

        if (lower.StartsWith("gpt-4-", StringComparison.Ordinal))
            return CreateTokenizerForEncoding(ModelEncoding.Cl100kBase);

        // Older defaults based on common legacy model names
        switch (lower)
        {
            case "code-davinci-001":
            case "code-davinci-002":
            case "text-davinci-002":
            case "text-davinci-003":
                return CreateTokenizerForEncoding(ModelEncoding.P50kBase);

            case "gpt-3.5-turbo":
            case "gpt-4":
                return CreateTokenizerForEncoding(ModelEncoding.Cl100kBase);

            case "gpt-4o":
                return CreateTokenizerForEncoding(ModelEncoding.O200kBase);
        }
 
        return CreateTokenizerForEncoding(ModelEncoding.Cl100kBase);
    }

    private static ITextTokenizer? CreateTokenizerForEncoding(ModelEncoding encoding) =>
        encoding switch
        {
            ModelEncoding.P50kBase => new P50KTokenizer(),
            ModelEncoding.P50kEdit => new P50KTokenizer(),
            ModelEncoding.Cl100kBase => new CL100KTokenizer(),
            ModelEncoding.O200kBase => new O200KTokenizer(),
            ModelEncoding.R50kBase => new P50KTokenizer(),
            ModelEncoding.GPT2 => new P50KTokenizer(),
            _ => null
        };
}