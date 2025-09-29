using Microsoft.AspNetCore.SignalR;

namespace API;

public class ClientToolHub : Hub
{
    private readonly ClientToolBroker _broker;

    public ClientToolHub(ClientToolBroker broker)
    {
        _broker = broker;
    }

    public async Task RegisterClient(RegisterClientRequest req)
    {
        Console.WriteLine($"Registering client for session {req.SessionId} with kernel {req.KernelId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, req.SessionId);
    }


    public Task SubmitToolResult(ToolResultEnvelope result)
    {
        Console.WriteLine($"Received tool result for {result.ToolCallId} from session {result.SessionId} with output: {result.OutputJson}");
        _broker.CompleteTool(result.ToolCallId, result);
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}

public record RegisterClientRequest(string SessionId, string KernelId);

public record ToolProgressEnvelope(
    string SessionId,
    string ToolCallId,
    string Name,
    string ProgressJson
);

public record ToolCallEnvelope(
    string SessionId,
    string ToolCallId,
    string Name,
    string ArgumentsJson
);

public record ToolResultEnvelope(
    string SessionId,
    string ToolName,
    string ToolCallId,
    string OutputJson,
    string? Error = null
);