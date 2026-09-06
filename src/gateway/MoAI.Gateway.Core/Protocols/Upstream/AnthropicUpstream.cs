using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;

namespace MoAI.Gateway.Protocols.Upstream;

/// <summary>
/// Anthropic Messages 上游协议.
/// </summary>
public class AnthropicUpstream : IUpstreamProtocol
{
    /// <inheritdoc/>
    public AIProtocolFamily Family => AIProtocolFamily.AnthropicMessages;

    /// <inheritdoc/>
    public HttpRequestMessage BuildRequest(string baseUrl, string apiKey, AiModelEntity model, GatewayChatRequest request)
    {
        var url = baseUrl.TrimEnd('/') + "/v1/messages";
        var body = new JsonObject
        {
            ["model"] = model.ModelId,
            ["messages"] = BuildMessages(request),
            ["max_tokens"] = request.MaxOutputTokens ?? 4096,
            ["stream"] = request.Stream,
        };

        var systemText = string.Join("\n\n", request.Messages
            .Where(x => x.Role == GatewayRole.System)
            .Select(x => x.Text ?? string.Empty)
            .Where(x => x.Length > 0));
        if (systemText.Length > 0)
        {
            body["system"] = systemText;
        }

        if (request.Tools != null)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = UpstreamHelper.SchemaToNode(tool.ParametersSchema),
                });
            }

            body["tools"] = tools;
            if (request.ToolChoice != null)
            {
                body["tool_choice"] = request.ToolChoice.Mode switch
                {
                    GatewayToolChoiceMode.None => new JsonObject { ["type"] = "none" },
                    GatewayToolChoiceMode.Required => new JsonObject { ["type"] = "any" },
                    GatewayToolChoiceMode.Function => new JsonObject
                    {
                        ["type"] = "tool",
                        ["name"] = request.ToolChoice.FunctionName,
                    },
                    _ => new JsonObject { ["type"] = "auto" },
                };
            }
        }

        if (request.Temperature.HasValue)
        {
            body["temperature"] = request.Temperature.Value;
        }

        if (request.TopP.HasValue)
        {
            body["top_p"] = request.TopP.Value;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            body["stop_sequences"] = new JsonArray(request.StopSequences.Select(x => JsonValue.Create(x)).ToArray());
        }

        var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        message.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        return message;
    }

    /// <inheritdoc/>
    public GatewayChatResponse ParseResponse(string body)
    {
        var root = UpstreamHelper.Parse(body);
        var response = new GatewayChatResponse
        {
            Id = GatewayJsonReader.String(root, "id") ?? GatewayIds.New("msg_"),
            Model = GatewayJsonReader.String(root, "model") ?? string.Empty,
        };

        var content = GatewayJsonReader.Array(root, "content");
        if (content != null)
        {
            foreach (var block in content.Value.EnumerateArray())
            {
                var type = GatewayJsonReader.String(block, "type");
                if (type == "text")
                {
                    var text = GatewayJsonReader.String(block, "text") ?? string.Empty;
                    response.Text = string.IsNullOrEmpty(response.Text) ? text : response.Text + text;
                }
                else if (type == "tool_use")
                {
                    response.ToolCalls.Add(new GatewayToolCall(
                        GatewayJsonReader.String(block, "id") ?? GatewayIds.New("call_"),
                        GatewayJsonReader.String(block, "name") ?? string.Empty,
                        GatewayJsonReader.Object(block, "input") is { } input ? input.GetRawText() : "{}"));
                }
            }
        }

        response.FinishReason = GatewayJsonReader.String(root, "stop_reason") switch
        {
            "max_tokens" => GatewayFinishReason.Length,
            "tool_use" => GatewayFinishReason.ToolCalls,
            "refusal" => GatewayFinishReason.ContentFilter,
            "stop_sequence" => GatewayFinishReason.Stop,
            _ => GatewayFinishReason.Stop,
        };

        if (GatewayJsonReader.Object(root, "usage") is { } usage)
        {
            response.Usage = new GatewayUsage(
                GatewayJsonReader.Int(usage, "input_tokens") ?? 0,
                GatewayJsonReader.Int(usage, "output_tokens") ?? 0);
        }

        return response;
    }

    /// <inheritdoc/>
    public IUpstreamSseParser CreateSseParser(string model)
    {
        return new AnthropicSseParser();
    }

    private static JsonArray BuildMessages(GatewayChatRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            if (message.Role == GatewayRole.System)
            {
                continue;
            }

            if (message.Role == GatewayRole.Tool)
            {
                var resultContent = new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = message.ToolCallId ?? string.Empty,
                    ["content"] = message.Text ?? string.Empty,
                };
                messages.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(resultContent),
                });
                continue;
            }

            var blocks = new JsonArray();
            if (message.Parts != null)
            {
                foreach (var part in message.Parts)
                {
                    if (part is GatewayTextPart textPart)
                    {
                        blocks.Add(new JsonObject { ["type"] = "text", ["text"] = textPart.Text });
                    }
                    else if (part is GatewayImagePart imagePart)
                    {
                        var image = ParseImageForAnthropic(imagePart);
                        if (image != null)
                        {
                            blocks.Add(image);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(message.Text))
            {
                blocks.Add(new JsonObject { ["type"] = "text", ["text"] = message.Text });
            }

            if (message.ToolCalls is { Count: > 0 })
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    JsonNode? input = null;
                    try
                    {
                        input = JsonNode.Parse(string.IsNullOrWhiteSpace(toolCall.ArgumentsJson) ? "{}" : toolCall.ArgumentsJson);
                    }
                    catch (JsonException)
                    {
                        input = new JsonObject();
                    }

                    blocks.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = toolCall.Id,
                        ["name"] = toolCall.Name,
                        ["input"] = input ?? new JsonObject(),
                    });
                }
            }

            if (blocks.Count == 0)
            {
                blocks.Add(new JsonObject { ["type"] = "text", ["text"] = string.Empty });
            }

            messages.Add(new JsonObject
            {
                ["role"] = message.Role == GatewayRole.Assistant ? "assistant" : "user",
                ["content"] = blocks,
            });
        }

        return messages;
    }

    private static JsonObject? ParseImageForAnthropic(GatewayImagePart part)
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
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = data,
                },
            };
        }

        if (part.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || part.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject { ["type"] = "url", ["url"] = part.Url },
            };
        }

        return null;
    }

    private sealed class AnthropicSseParser : IUpstreamSseParser
    {
        private GatewayUsage? _usage;
        private GatewayFinishReason _finishReason = GatewayFinishReason.Stop;
        private int _nextToolIndex;
        private readonly Dictionary<int, int> _toolIndexByBlockIndex = new();

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

            var type = GatewayJsonReader.String(root, "type");
            switch (type)
            {
                case "message_start":
                {
                    var message = GatewayJsonReader.Object(root, "message") ?? default;
                    yield return new GatewayStreamEvent.Started(
                        GatewayJsonReader.String(message, "id") ?? GatewayIds.New("msg_"),
                        GatewayJsonReader.String(message, "model") ?? string.Empty);
                    break;
                }

                case "content_block_start":
                {
                    var index = GatewayJsonReader.Int(root, "index") ?? 0;
                    if (GatewayJsonReader.Object(root, "content_block") is { } block
                        && GatewayJsonReader.String(block, "type") == "tool_use")
                    {
                        var toolIndex = _nextToolIndex++;
                        _toolIndexByBlockIndex[index] = toolIndex;
                        yield return new GatewayStreamEvent.ToolCallStarted(
                            toolIndex,
                            GatewayJsonReader.String(block, "id") ?? GatewayIds.New("call_"),
                            GatewayJsonReader.String(block, "name") ?? string.Empty);
                    }

                    break;
                }

                case "content_block_delta":
                {
                    var index = GatewayJsonReader.Int(root, "index") ?? 0;
                    if (GatewayJsonReader.Object(root, "delta") is { } delta)
                    {
                        var deltaType = GatewayJsonReader.String(delta, "type");
                        if (deltaType == "text_delta" && GatewayJsonReader.String(delta, "text") is { } text)
                        {
                            yield return new GatewayStreamEvent.TextDelta(text);
                        }
                        else if (deltaType == "input_json_delta" && GatewayJsonReader.String(delta, "partial_json") is { } partialJson)
                        {
                            var toolIndex = _toolIndexByBlockIndex.TryGetValue(index, out var mapped) ? mapped : _nextToolIndex - 1;
                            yield return new GatewayStreamEvent.ToolCallArgumentsDelta(Math.Max(0, toolIndex), partialJson);
                        }
                    }

                    break;
                }

                case "message_delta":
                {
                    if (GatewayJsonReader.Object(root, "delta") is { } delta
                        && GatewayJsonReader.String(delta, "stop_reason") is { } stopReason)
                    {
                        _finishReason = stopReason switch
                        {
                            "max_tokens" => GatewayFinishReason.Length,
                            "tool_use" => GatewayFinishReason.ToolCalls,
                            "refusal" => GatewayFinishReason.ContentFilter,
                            _ => GatewayFinishReason.Stop,
                        };
                    }

                    if (GatewayJsonReader.Object(root, "usage") is { } usage)
                    {
                        var promptTokens = _usage?.PromptTokens ?? GatewayJsonReader.Int(usage, "input_tokens") ?? 0;
                        _usage = new GatewayUsage(promptTokens, GatewayJsonReader.Int(usage, "output_tokens") ?? 0);
                    }

                    break;
                }

                case "message_stop":
                    yield return new GatewayStreamEvent.Finished(_finishReason, _usage);
                    break;
            }
        }
    }
}
