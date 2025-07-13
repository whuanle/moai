// <copyright file="AiProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MoAI.AiModel.Models;

/// <summary>
/// AI 服务提供商，服务商来源.
/// </summary>
public enum AiProvider
{
    /// <summary>
    /// 自定义.
    /// </summary>
    [JsonPropertyName("custom")]
    [EnumMember(Value = "custom")]
    Custom,

    /// <summary>
    /// AI21 Labs.
    /// </summary>
    [JsonPropertyName("ai21")]
    [EnumMember(Value = "ai21")]
    Ai21,

    /// <summary>
    /// 360 AI.
    /// </summary>
    [JsonPropertyName("ai360")]
    [EnumMember(Value = "ai360")]
    Ai360,

    /// <summary>
    /// Anthropic.
    /// </summary>
    [JsonPropertyName("anthropic")]
    [EnumMember(Value = "anthropic")]
    Anthropic,

    /// <summary>
    /// Azure, Azure == AzureAI.
    /// </summary>
    [JsonPropertyName("azure")]
    [EnumMember(Value = "azure")]
    Azure,

    /// <summary>
    /// 百川智能.
    /// </summary>
    [JsonPropertyName("baichuan")]
    [EnumMember(Value = "baichuan")]
    Baichuan,

    /// <summary>
    /// Bedrock.
    /// </summary>
    [JsonPropertyName("bedrock")]
    [EnumMember(Value = "bedrock")]
    Bedrock,

    /// <summary>
    /// Cloudflare.
    /// </summary>
    [JsonPropertyName("cloudflare")]
    [EnumMember(Value = "cloudflare")]
    Cloudflare,

    /// <summary>
    /// Cohere.
    /// </summary>
    [JsonPropertyName("cohere")]
    [EnumMember(Value = "cohere")]
    Cohere,

    /// <summary>
    /// DeepSeek.
    /// </summary>
    [JsonPropertyName("deepseek")]
    [EnumMember(Value = "deepseek")]
    DeepSeek,

    /// <summary>
    /// Fireworks AI.
    /// </summary>
    [JsonPropertyName("fireworksai")]
    [EnumMember(Value = "fireworksai")]
    FireworksAI,

    /// <summary>
    /// Gitee AI.
    /// </summary>
    [JsonPropertyName("giteeai")]
    [EnumMember(Value = "giteeai")]
    GiteeAI,

    /// <summary>
    /// GitHub.
    /// </summary>
    [JsonPropertyName("github")]
    [EnumMember(Value = "github")]
    GitHub,

    /// <summary>
    /// Google.
    /// </summary>
    [JsonPropertyName("google")]
    [EnumMember(Value = "google")]
    Google,

    /// <summary>
    /// Groq.
    /// </summary>
    [JsonPropertyName("groq")]
    [EnumMember(Value = "groq")]
    Groq,

    /// <summary>
    /// Higress.
    /// </summary>
    [JsonPropertyName("higress")]
    [EnumMember(Value = "higress")]
    Higress,

    /// <summary>
    /// HuggingFace.
    /// </summary>
    [JsonPropertyName("huggingface")]
    [EnumMember(Value = "huggingface")]
    HuggingFace,

    /// <summary>
    /// 腾讯 Hunyuan.
    /// </summary>
    [JsonPropertyName("hunyuan")]
    [EnumMember(Value = "hunyuan")]
    Hunyuan,

    /// <summary>
    /// InfinitiAI.
    /// </summary>
    [JsonPropertyName("infiniai")]
    [EnumMember(Value = "infiniai")]
    InfinitiAI,

    /// <summary>
    /// InternLM.
    /// </summary>
    [JsonPropertyName("internlm")]
    [EnumMember(Value = "internlm")]
    InternLM,

    /// <summary>
    /// Jina AI.
    /// </summary>
    [JsonPropertyName("jina")]
    [EnumMember(Value = "jina")]
    Jina,

    /// <summary>
    /// LM Studio.
    /// </summary>
    [JsonPropertyName("lmstudio")]
    [EnumMember(Value = "lmstudio")]
    LMStudio,

    /// <summary>
    /// MiniMax.
    /// </summary>
    [JsonPropertyName("minimax")]
    [EnumMember(Value = "minimax")]
    MiniMax,

    /// <summary>
    /// Mistral.
    /// </summary>
    [JsonPropertyName("mistral")]
    [EnumMember(Value = "mistral")]
    Mistral,

    /// <summary>
    /// Moonshot.
    /// </summary>
    [JsonPropertyName("moonshot")]
    [EnumMember(Value = "moonshot")]
    Moonshot,

    /// <summary>
    /// Novita AI.
    /// </summary>
    [JsonPropertyName("novita")]
    [EnumMember(Value = "novita")]
    Novita,

    /// <summary>
    /// NVIDIA.
    /// </summary>
    [JsonPropertyName("nvidia")]
    [EnumMember(Value = "nvidia")]
    Nvidia,

    /// <summary>
    /// Ollama.
    /// </summary>
    [JsonPropertyName("ollama")]
    [EnumMember(Value = "ollama")]
    Ollama,

    /// <summary>
    /// OpenAI.
    /// </summary>
    [JsonPropertyName("openai")]
    [EnumMember(Value = "openai")]
    OpenAI,

    /// <summary>
    /// OpenRouter.
    /// </summary>
    [JsonPropertyName("openrouter")]
    [EnumMember(Value = "openrouter")]
    OpenRouter,

    /// <summary>
    /// Perplexity.
    /// </summary>
    [JsonPropertyName("perplexity")]
    [EnumMember(Value = "perplexity")]
    Perplexity,

    /// <summary>
    /// PPIO.
    /// </summary>
    [JsonPropertyName("ppio")]
    [EnumMember(Value = "ppio")]
    PPIO,

    /// <summary>
    /// Qiniu.
    /// </summary>
    [JsonPropertyName("qiniu")]
    [EnumMember(Value = "qiniu")]
    Qiniu,

    /// <summary>
    /// Qwen.
    /// </summary>
    [JsonPropertyName("qwen")]
    [EnumMember(Value = "qwen")]
    Qwen,

    /// <summary>
    /// SambaNova.
    /// </summary>
    [JsonPropertyName("sambanova")]
    [EnumMember(Value = "sambanova")]
    SambaNova,

    /// <summary>
    /// Search1API.
    /// </summary>
    [JsonPropertyName("search1api")]
    [EnumMember(Value = "search1api")]
    Search1API,

    /// <summary>
    /// SenseNova.
    /// </summary>
    [JsonPropertyName("sensenova")]
    [EnumMember(Value = "sensenova")]
    SenseNova,

    /// <summary>
    /// SiliconCloud.
    /// </summary>
    [JsonPropertyName("siliconcloud")]
    [EnumMember(Value = "siliconcloud")]
    SiliconCloud,

    /// <summary>
    /// Spark 星火大模型.
    /// </summary>
    [JsonPropertyName("spark")]
    [EnumMember(Value = "spark")]
    Spark,

    /// <summary>
    /// StepFun.
    /// </summary>
    [JsonPropertyName("stepfun")]
    [EnumMember(Value = "stepfun")]
    StepFun,

    /// <summary>
    /// Taichu.
    /// </summary>
    [JsonPropertyName("taichu")]
    [EnumMember(Value = "taichu")]
    Taichu,

    /// <summary>
    /// 腾讯云 TencentCloud.
    /// </summary>
    [JsonPropertyName("tencentcloud")]
    [EnumMember(Value = "tencentcloud")]
    TencentCloud,

    /// <summary>
    /// Together AI.
    /// </summary>
    [JsonPropertyName("togetherai")]
    [EnumMember(Value = "togetherai")]
    TogetherAI,

    /// <summary>
    /// Upstage.
    /// </summary>
    [JsonPropertyName("upstage")]
    [EnumMember(Value = "upstage")]
    Upstage,

    /// <summary>
    /// Vertex AI.
    /// </summary>
    [JsonPropertyName("vertexai")]
    [EnumMember(Value = "vertexai")]
    VertexAI,

    /// <summary>
    /// vLLM.
    /// </summary>
    [JsonPropertyName("vllm")]
    [EnumMember(Value = "vllm")]
    Vllm,

    /// <summary>
    /// Volcengine.
    /// </summary>
    [JsonPropertyName("volcengine")]
    [EnumMember(Value = "volcengine")]
    Volcengine,

    /// <summary>
    /// 文心 Wenxin.
    /// </summary>
    [JsonPropertyName("wenxin")]
    [EnumMember(Value = "wenxin")]
    Wenxin,

    /// <summary>
    /// xAI.
    /// </summary>
    [JsonPropertyName("xai")]
    [EnumMember(Value = "xai")]
    XAI,

    /// <summary>
    /// Xinference.
    /// </summary>
    [JsonPropertyName("xinference")]
    [EnumMember(Value = "xinference")]
    Xinference,

    /// <summary>
    /// ZeroOne.
    /// </summary>
    [JsonPropertyName("zeroone")]
    [EnumMember(Value = "zeroone")]
    ZeroOne,

    /// <summary>
    /// 智谱 AI Zhipu.
    /// </summary>
    [JsonPropertyName("zhipu")]
    [EnumMember(Value = "zhipu")]
    Zhipu,
}
