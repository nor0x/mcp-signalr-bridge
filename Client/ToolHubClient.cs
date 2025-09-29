using Microsoft.AspNetCore.SignalR.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace Client;

public class ToolHubClient
{
    private readonly HubConnection _conn;
    private readonly string _sessionId;
    private readonly Dictionary<string, McpServerTool> _plugins;

    public ToolHubClient(Uri hubUrl, string sessionId, IEnumerable<McpServerTool> plugins, McpServer server)
    {
        _sessionId = sessionId;
        _plugins = plugins.ToDictionary(p => p.ProtocolTool.Name);

        var builder = new HubConnectionBuilder()
            .WithUrl(new Uri(hubUrl, "toolhub"))
            .WithAutomaticReconnect();

        _conn = builder.Build();

        _conn.On<ToolCallEnvelope>("ToolCall", async envelope =>
        {
            if (envelope.SessionId != _sessionId) return;

            string outputJson;
            string? error = null;

            try
            {
                if (_plugins.TryGetValue(envelope.Name, out var plugin))
                {
                    using var doc = JsonDocument.Parse(envelope.ArgumentsJson);

                    var deserializedArgs = JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(envelope.ArgumentsJson);
                    var toolParams = new CallToolRequestParams()
                    {
                        Name = envelope.Name,
                        Arguments = deserializedArgs ?? new Dictionary<string, JsonElement>()
                    };
                    var requestContext = new RequestContext<CallToolRequestParams>(server, new JsonRpcRequest
                    {
                        Id = new RequestId("test-id"),
                        Method = "test/method",
                        Params = null
                    });

                    requestContext.Params = toolParams;
                    var toolResult = await plugin.InvokeAsync(requestContext);

                    if (toolResult.IsError == true)
                    {
                        outputJson = $"Error in {envelope.Name}: {JsonSerializer.Serialize(toolResult.Content)}";
                    }
                    else
                    {
                        outputJson = $"{envelope.Name} executed successfully. Result: {JsonSerializer.Serialize(toolResult.Content)}";
                    }

                }
                else
                {
                    error = $"Unknown client plugin: {envelope.Name}";
                    outputJson = JsonSerializer.Serialize(new { error });
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                outputJson = JsonSerializer.Serialize(new { error });
            }

            var result = new ToolResultEnvelope(envelope.Name, envelope.SessionId, envelope.ToolCallId, outputJson, error);
            await _conn.InvokeAsync("SubmitToolResult", result);
        });
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _conn.StartAsync(ct);
        await _conn.InvokeAsync("RegisterClient", new RegisterClientRequest(_sessionId), ct);
    }

}

public record ToolCallEnvelope(
    string SessionId,
    string ToolCallId,
    string Name,
    string ArgumentsJson
);

public record ToolResultEnvelope(
    string ToolName,
    string SessionId,
    string ToolCallId,
    string OutputJson,
    string? Error = null
);

public record RegisterClientRequest(string SessionId);
