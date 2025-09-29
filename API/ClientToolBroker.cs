using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace API;

public class ClientToolBroker
{
    private readonly IHubContext<ClientToolHub> _hubContext;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ToolResultEnvelope>> _pending =
        new(StringComparer.Ordinal);

    public ClientToolBroker(IHubContext<ClientToolHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task<ToolResultEnvelope> RequestClientToolAsync(
        ToolCallEnvelope call,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<ToolResultEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(call.ToolCallId, tcs))
            throw new InvalidOperationException($"Duplicate Tool Call {call.ToolCallId}");

        try
        {
            await _hubContext.Clients.Group(call.SessionId).SendAsync("ToolCall", call, cancellationToken);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(call.ToolCallId, out _);
        }
    }

    public void CompleteTool(string toolCallId, ToolResultEnvelope result)
    {
        if (_pending.TryRemove(toolCallId, out var tcs))
            tcs.TrySetResult(result);
    }
}
