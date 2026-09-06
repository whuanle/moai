using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;

namespace MoAI.Gateway.Protocols.Upstream;

/// <summary>
/// OpenAI Responses 上游协议.
/// </summary>
public class OpenAIResponsesUpstream : IUpstreamProtocol
{
    /// <inheritdoc/>
    public AIProtocolFamily Family => AIProtocolFamily.OpenAIResponses;

    /// <inheritdoc/>
    public HttpRequestMessage BuildRequest(string baseUrl, string apiKey, AiModelEntity model, GatewayChatRequest request)
    {
        var url = UpstreamHelper.NormalizeOpenAiBase(baseUrl) + "/responses";
        var body = new JsonObject
        {
            ["model"] = model.ModelId,
            ["input"] = BuildInput(request),
            ["stream"] = request.Stream,
        };

        var systemText = string.Join("\n\n", request.Messages
            .Where(x => x.Role == GatewayRole.System)
            .Select(x => x.Text ?? string.Empty)
            .Where(x => x.Length > 0));
        if (systemText.Length > 0)
        {
            body["instructions"] = systemText;
        }

        if (request.Tools != null)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = UpstreamHelper.SchemaToNode(tool.ParametersSchema),
                });
            }

            body["tools"] = tools;
            if (request.ToolChoice != null)
            {
                body["tool_choice"] = request.ToolChoice.Mode switch
                {
                    GatewayToolChoiceMode.None => "none",
                    GatewayToolChoiceMode.Required => "required",
                    GatewayToolChoiceMode.Function => new JsonObject
                    {
                        ["type"] = "function",
                        ["name"] = request.ToolChoice.FunctionName,
                    },
                    _ => "auto",
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

        if (request.MaxOutputTokens.HasValue)
        {
            body["max_output_tokens"] = request.MaxOutputTokens.Value;
        }

        if (!string.IsNullOrEmpty(request.User))
        {
            body["user"] = request.User;
        }

        var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return message;
    }

    /// <inheritdoc/>
    public GatewayChatResponse ParseResponse(string body)
    {
        var root = UpstreamHelper.Parse(body);
        var response = new GatewayChatResponse
        {
            Id = GatewayJsonReader.String(root, "id") ?? GatewayIds.New("resp_"),
            Model = GatewayJsonReader.String(root, "model") ?? string.Empty,
        };

        var output = GatewayJsonReader.Array(root, "output");
        if (output != null)
        {
            foreach (var item in output.Value.EnumerateArray())
            {
                var type = GatewayJsonReader.String(item, "type");
                if (type == "message")
                {
                    var content = GatewayJsonReader.Array(item, "content");
                    if (content == null)
                    {
                        continue;
                    }

                    foreach (var part in content.Value.EnumerateArray())
                    {
                        if (GatewayJsonReader.String(part, "type") is "output_text" or "text"
                            && GatewayJsonReader.String(part, "text") is { } text)
                        {
                            response.Text = string.IsNullOrEmpty(response.Text) ? text : response.Text + text;
                        }
                    }
                }
                else if (type == "function_call")
                {
                    response.ToolCalls.Add(new GatewayToolCall(
                        GatewayJsonReader.String(item, "call_id") ?? GatewayIds.New("call_"),
                        GatewayJsonReader.String(item, "name") ?? string.Empty,
                        GatewayJsonReader.String(item, "arguments") ?? "{}"));
                }
            }
        }

        response.FinishReason = response.ToolCalls.Count > 0
            ? GatewayFinishReason.ToolCalls
            : GatewayJsonReader.String(root, "status") == "incomplete"
                ? GatewayFinishReason.Length
                : GatewayFinishReason.Stop;

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
        return new ResponsesSseParser();
    }

    private static JsonArray BuildInput(GatewayChatRequest request)
    {
        var input = new JsonArray();
        foreach (var message in request.Messages)
        {
            if (message.Role == GatewayRole.System)
            {
                // system 已并入 instructions.
                continue;
            }

            if (message.Role == GatewayRole.Tool)
            {
                input.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = message.ToolCallId ?? string.Empty,
                    ["output"] = message.Text ?? string.Empty,
                });
                continue;
            }

            if (message.ToolCalls is { Count: > 0 })
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    input.Add(new JsonObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = toolCall.Id,
                        ["name"] = toolCall.Name,
                        ["arguments"] = toolCall.ArgumentsJson,
                    });
                }

                continue;
            }

            var obj = new JsonObject
            {
                ["type"] = "message",
                ["role"] = message.Role == GatewayRole.Assistant ? "assistant" : "user",
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = message.Role == GatewayRole.Assistant ? "output_text" : "input_text",
                    ["text"] = message.Text ?? string.Empty,
                }),
            };
            input.Add(obj);
        }

        return input;
    }

    private sealed class ResponsesSseParser : IUpstreamSseParser
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

            var type = GatewayJsonReader.String(root, "type");
            switch (type)
            {
                case "response.created":
                    if (!_started)
                    {
                        _started = true;
                        var response = GatewayJsonReader.Object(root, "response") ?? default;
                        yield return new GatewayStreamEvent.Started(
                            GatewayJsonReader.String(response, "id") ?? GatewayIds.New("resp_"),
                            GatewayJsonReader.String(response, "model") ?? string.Empty);
                    }

                    break;

                case "response.output_text.delta":
                {
                    var delta = GatewayJsonReader.String(root, "delta");
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return new GatewayStreamEvent.TextDelta(delta);
                    }

                    break;
                }

                case "response.output_item.added":
                {
                    if (GatewayJsonReader.Object(root, "item") is { } item
                        && GatewayJsonReader.String(item, "type") == "function_call")
                    {
                        var index = _nextToolIndex++;
                        yield return new GatewayStreamEvent.ToolCallStarted(
                            index,
                            GatewayJsonReader.String(item, "call_id") ?? GatewayIds.New("call_"),
                            GatewayJsonReader.String(item, "name") ?? string.Empty);
                    }

                    break;
                }

                case "response.function_call_arguments.delta":
                {
                    var delta = GatewayJsonReader.String(root, "delta");
                    if (!string.IsNullOrEmpty(delta))
                    {
                        // 事件未携带工具序号，挂到当前最后一个工具调用.
                        yield return new GatewayStreamEvent.ToolCallArgumentsDelta(Math.Max(0, _nextToolIndex - 1), delta);
                    }

                    break;
                }

                case "response.completed":
                case "response.incomplete":
                {
                    if (GatewayJsonReader.Object(root, "response") is { } response)
                    {
                        if (GatewayJsonReader.Object(response, "usage") is { } usage)
                        {
                            _usage = new GatewayUsage(
                                GatewayJsonReader.Int(usage, "input_tokens") ?? 0,
                                GatewayJsonReader.Int(usage, "output_tokens") ?? 0);
                        }

                        if (type == "response.incomplete"
                            || GatewayJsonReader.String(response, "status") == "incomplete")
                        {
                            _finishReason = GatewayFinishReason.Length;
                        }
                    }

                    yield return new GatewayStreamEvent.Finished(_finishReason, _usage);
                    break;
                }

                case "response.failed":
                case "error":
                {
                    var message = "Upstream response failed.";
                    if (GatewayJsonReader.Object(root, "response") is { } response
                        && GatewayJsonReader.Object(response, "error") is { } error)
                    {
                        message = GatewayJsonReader.String(error, "message") ?? message;
                    }
                    else if (GatewayJsonReader.Object(root, "error") is { } rootError)
                    {
                        message = GatewayJsonReader.String(rootError, "message") ?? message;
                    }

                    throw new GatewayProtocolException(502, message, "upstream_error");
                }
            }
        }
    }
}
