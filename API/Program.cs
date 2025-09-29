using API;
using System.ComponentModel;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ClientToolBroker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHub<ClientToolHub>("/toolhub");
app.UseHttpsRedirection();

app.MapGet("/chat", (string input) => StreamChat(input));

async IAsyncEnumerable<string> StreamChat(string input)
{
    IChatClient openaiClient =
        new OpenAI.Chat.ChatClient("gpt-5-nano", "my-api-key")
        .AsIChatClient();

    IChatClient client = new ChatClientBuilder(openaiClient)
        .UseFunctionInvocation()
        .Build();

    ChatOptions chatOptions = new()
    {
        Tools = [AIFunctionFactory.Create(WriteDateTimeToDesktopAsync)]
    };

    await foreach (var message in client.GetStreamingResponseAsync(input, chatOptions))
    {
        Console.Write(message);
        yield return message.Text;
    }
}

app.Run();


[Description("creates a file on the desktop with the current date and time - returns the date time string")]
async Task<string> WriteDateTimeToDesktopAsync(string filename)
{
    var clientToolBroker = app.Services.GetRequiredService<ClientToolBroker>();
    var result = await clientToolBroker.RequestClientToolAsync(
        new ToolCallEnvelope(
            SessionId: "default-session",
            ToolCallId: Guid.NewGuid().ToString(),
            Name: "WriteDateTimeToDesktopAsync",
            ArgumentsJson: $"{{ \"filename\": \"{filename}\" }}"),
        timeout: TimeSpan.FromMinutes(2));
    if (result.Error is not null)
        throw new InvalidOperationException(result.Error);
    return result.OutputJson;
}
