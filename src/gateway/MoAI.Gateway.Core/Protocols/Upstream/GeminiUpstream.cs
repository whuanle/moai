using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;

namespace MoAI.Gateway.Protocols.Upstream;

/// <summary>
/// Google Gemini 上游协议（v1beta generateContent / streamGenerateContent）.
/// </summary>
public class GeminiUpstream : IUpstreamProtocol
{
    /// <inheritdoc/>
    public AIProtocolFamily Family => AIProtocolFamily.GoogleGemini;

    /// <inheritdoc/>
    public HttpRequestMessage BuildRequest(string baseUrl, string apiKey, AiModelEntity model, GatewayChatRequest request)
    {
        var baseTrimmed = baseUrl.TrimEnd('/');
        var methodName = request.Stream ? "streamGenerateContent?alt=sse" : "generateContent";
        var url = $"{baseTrimmed}/v1beta/models/{Uri.EscapeDataString(model.ModelId)}:{methodName}";

        var body = new JsonObject
        {
            ["contents"] = BuildContents(request),
        };

        var systemText = string.Join("\n\n", request.Messages
            .Where(x => x.Role == GatewayRole.System)
            .Select(x => x.Text ?? string.Empty)
            .Where(x => x.Length > 0));
        if (systemText.Length > 0)
        {
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = systemText }),
            };
        }

        if (request.Tools != null)
        {
            var declarations = new JsonArray();
            foreach (var tool in request.Tools)
            {
                var declaration = new JsonObject { ["name"] = tool.Name };
                if (!string.IsNullOrEmpty(tool.Description))
                {
                    declaration["description"] = tool.Description;
                }

                declaration["parameters"] = UpstreamHelper.SchemaToNode(tool.ParametersSchema);
                declarations.Add(declaration);
            }

            body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });
            if (request.ToolChoice != null)
            {
                var config = new JsonObject
                {
                    ["mode"] = request.ToolChoice.Mode switch
                    {
                        GatewayToolChoiceMode.None => "NONE",
                        GatewayToolChoiceMode.Required => "ANY",
                        GatewayToolChoiceMode.Function => "ANY",
                        _ => "AUTO",
                    },
                };
                if (request.ToolChoice.Mode == GatewayToolChoiceMode.Function)
                {
                    config["allowedFunctionNames"] = new JsonArray(JsonValue.Create(request.ToolChoice.FunctionName));
                }

                body["toolConfig"] = new JsonObject { ["functionCallingConfig"] = config };
            }
        }

        var generationConfig = new JsonObject();
        if (request.Temperature.HasValue)
        {
            generationConfig["temperature"] = request.Temperature.Value;
        }

        if (request.TopP.HasValue)
        {
            generationConfig["topP"] = request.TopP.Value;
        }

        if (request.MaxOutputTokens.HasValue)
        {
            generationConfig["maxOutputTokens"] = request.MaxOutputTokens.Value;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            generationConfig["stopSequences"] = new JsonArray(request.StopSequences.Select(x => JsonValue.Create(x)).ToArray());
        }

        body["generationConfig"] = generationConfig;

        var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        return message;
    }

    /// <inheritdoc/>
    public GatewayChatResponse ParseResponse(string body)
    {
        var root = UpstreamHelper.Parse(body);
        return ReadResponse(root, GatewayIds.New("gemini-"));
    }

    /// <inheritdoc/>
    public IUpstreamSseParser CreateSseParser(string model)
    {
        return new GeminiSseParser();
    }

    private static GatewayChatResponse ReadResponse(JsonElement root, string fallbackId)
    {
        var response = new GatewayChatResponse { Id = fallbackId };

        var candidates = GatewayJsonReader.Array(root, "candidates");
        var candidate = GatewayJsonReader.First(candidates);
        if (candidate.ValueKind == JsonValueKind.Object)
        {
            var content = GatewayJsonReader.Object(candidate, "content");
            var parts = content == null ? null : GatewayJsonReader.Array(content.Value, "parts");
            if (parts != null)
            {
                foreach (var part in parts.Value.EnumerateArray())
                {
                    if (GatewayJsonReader.String(part, "text") is { } text)
                    {
                        response.Text = string.IsNullOrEmpty(response.Text) ? text : response.Text + text;
                    }
                    else if (GatewayJsonReader.Object(part, "functionCall") is { } functionCall)
                    {
                        response.ToolCalls.Add(new GatewayToolCall(
                            GatewayIds.New("call_"),
                            GatewayJsonReader.String(functionCall, "name") ?? string.Empty,
                            GatewayJsonReader.Object(functionCall, "args") is { } args ? args.GetRawText() : "{}"));
                    }
                }
            }

            response.FinishReason = GatewayJsonReader.String(candidate, "finishReason") switch
            {
                "MAX_TOKENS" => GatewayFinishReason.Length,
                "SAFETY" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII" => GatewayFinishReason.ContentFilter,
                _ => response.ToolCalls.Count > 0 ? GatewayFinishReason.ToolCalls : GatewayFinishReason.Stop,
            };
        }

        if (GatewayJsonReader.Object(root, "usageMetadata") is { } usage)
        {
            response.Usage = new GatewayUsage(
                GatewayJsonReader.Int(usage, "promptTokenCount") ?? 0,
                GatewayJsonReader.Int(usage, "candidatesTokenCount") ?? 0);
        }

        return response;
    }

    private static JsonArray BuildContents(GatewayChatRequest request)
    {
        var contents = new JsonArray();
        foreach (var message in request.Messages)
        {
            if (message.Role == GatewayRole.System)
            {
                continue;
            }

            var role = message.Role == GatewayRole.Assistant ? "model" : "user";
            var parts = new JsonArray();

            if (message.Role == GatewayRole.Tool)
            {
                var response = new JsonObject();
                try
                {
                    var parsed = JsonNode.Parse(string.IsNullOrWhiteSpace(message.Text) ? "{}" : message.Text);
                    response["result"] = parsed ?? new JsonObject();
                }
                catch (JsonException)
                {
                    response["result"] = message.Text ?? string.Empty;
                }

                parts.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = message.Name ?? message.ToolCallId ?? string.Empty,
                        ["response"] = response,
                    },
                });
            }
            else
            {
                if (message.Parts != null)
                {
                    foreach (var part in message.Parts)
                    {
                        if (part is GatewayTextPart textPart)
                        {
                            parts.Add(new JsonObject { ["text"] = textPart.Text });
                        }
                        else if (part is GatewayImagePart imagePart && ParseImageForGemini(imagePart) is { } image)
                        {
                            parts.Add(image);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(message.Text))
                {
                    parts.Add(new JsonObject { ["text"] = message.Text });
                }

                if (message.ToolCalls is { Count: > 0 })
                {
                    foreach (var toolCall in message.ToolCalls)
                    {
                        JsonNode? args = null;
                        try
                        {
                            args = JsonNode.Parse(string.IsNullOrWhiteSpace(toolCall.ArgumentsJson) ? "{}" : toolCall.ArgumentsJson);
                        }
                        catch (JsonException)
                        {
                            args = new JsonObject();
                        }

                        parts.Add(new JsonObject
                        {
                            ["functionCall"] = new JsonObject
                            {
                                ["name"] = toolCall.Name,
                                ["args"] = args ?? new JsonObject(),
                            },
                        });
                    }
                }
            }

            if (parts.Count == 0)
            {
                parts.Add(new JsonObject { ["text"] = string.Empty });
            }

            contents.Add(new JsonObject { ["role"] = role, ["parts"] = parts });
        }

        return contents;
    }

    private static JsonObject? ParseImageForGemini(GatewayImagePart part)
    {
        const string dataPrefix = "data:";
        if (part.Url.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = part.Url.IndexOf(',');
            if (commaIndex < 0)
            {
                return null;
            }

            var header = part.Url[dataPrefix.Length..commaIndex];
            var mediaType = header.Split(';')[0];
            var data = part.Url[(commaIndex + 1)..];
            return new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["mimeType"] = mediaType,
                    ["data"] = data,
                },
            };
        }

        if (part.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || part.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["fileData"] = new JsonObject
                {
                    ["fileUri"] = part.Url,
                },
            };
        }

        return null;
    }

    private sealed class GeminiSseParser : IUpstreamSseParser
    {
        private GatewayUsage? _usage;
        private GatewayFinishReason _finishReason = GatewayFinishReason.Stop;
        private bool _started;
        private int _nextToolIndex;

        public IEnumerable<GatewayStreamEvent> Feed(string data)
        {
            JsonElement root;
            try
            {
                root = JsonDocument.Parse(data).RootElement.Clone();
            }
            catch (JsonException)
            {
                yield break;
            }

            if (!_started)
            {
                _started = true;
                yield return new GatewayStreamEvent.Started(GatewayIds.New("gemini-"), string.Empty);
            }

            var response = ReadResponse(root, GatewayIds.New("gemini-"));
            if (!string.IsNullOrEmpty(response.Text))
            {
                yield return new GatewayStreamEvent.TextDelta(response.Text);
            }

            foreach (var toolCall in response.ToolCalls)
            {
                var index = _nextToolIndex++;
                yield return new GatewayStreamEvent.ToolCallStarted(index, toolCall.Id, toolCall.Name);
                yield return new GatewayStreamEvent.ToolCallArgumentsDelta(index, toolCall.ArgumentsJson);
            }

            if (response.Usage != null)
            {
                _usage = response.Usage;
            }

            var candidate = GatewayJsonReader.First(GatewayJsonReader.Array(root, "candidates"));
            var finishReason = candidate.ValueKind == JsonValueKind.Object ? GatewayJsonReader.String(candidate, "finishReason") : null;
            if (!string.IsNullOrEmpty(finishReason))
            {
                _finishReason = finishReason switch
                {
                    "MAX_TOKENS" => GatewayFinishReason.Length,
                    "SAFETY" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII" => GatewayFinishReason.ContentFilter,
                    _ => GatewayFinishReason.Stop,
                };
                yield return new GatewayStreamEvent.Finished(_finishReason, _usage);
            }
        }
    }
}
