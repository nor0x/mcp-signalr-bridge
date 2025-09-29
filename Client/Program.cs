using Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

Console.WriteLine("Starting MCP client and server...");

ToolHubClient? _toolHubClient = null;
string _sessionId = "default-session";

try
{
	var (client, server) = await SetupMcpServerAndClient();
	Console.WriteLine("MCP server and client setup complete.");
	var tools = await client.ListToolsAsync(null, CancellationToken.None);
	Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");
	Console.WriteLine("Starting ToolHub client, to receive tool requests from the API...");
	await StartToolHubClient(server);
	Console.WriteLine("ToolHub client started.");

	while (true)
	{
		Console.WriteLine();
		Console.WriteLine("Enter a prompt (or /exit to quit): ");
		Console.WriteLine();
		var prompt = Console.ReadLine();
		if (prompt == null || prompt.Trim().ToLower() == "/exit")
		{
			break;
		}

		using var httpClient = new HttpClient();

		var response = await httpClient.GetAsync($"http://localhost:5181/chat?input={prompt}", HttpCompletionOption.ResponseHeadersRead);
		var responseStream = await response.Content.ReadAsStreamAsync();

		var jsonResponse = JsonSerializer.DeserializeAsyncEnumerable<string>(responseStream);
		Console.WriteLine("Response:");
		await foreach (var item in jsonResponse)
		{
			Console.Write(item);
		}
	}

}
catch (Exception ex)
{
	Console.WriteLine($"Error during setup: {ex}");
	return;
}
finally
{
	Console.ReadKey();
}



async Task<(McpClient client, McpServer server)> SetupMcpServerAndClient()
{
	var builder = Host.CreateEmptyApplicationBuilder(settings: null);

	builder.Logging.AddConsole(consoleLogOptions =>
	{
		consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
	});

	builder.Logging.AddDebug();

	var clientPipe = new Pipe();
	var serverPipe = new Pipe();

	builder.Services.AddMcpServer()
	.WithStreamServerTransport(inputStream: serverPipe.Reader.AsStream(), outputStream: clientPipe.Writer.AsStream())
	.WithTools<LocalTools>();

	var app = builder.Build();
	var _ = app.RunAsync();

	var loggerFactory = LoggerFactory.Create(loggingBuilder =>
	{
		loggingBuilder.AddDebug();
		loggingBuilder.AddConsole();
		loggingBuilder.SetMinimumLevel(LogLevel.Trace);
	});

	var client = await McpClient.CreateAsync(
	clientTransport: new StreamClientTransport(
		serverInput: serverPipe.Writer.AsStream(),
		serverOutput: clientPipe.Reader.AsStream()),
	loggerFactory: loggerFactory,
	cancellationToken: CancellationToken.None);

	var server = app.Services.GetService<McpServer>();
	return (client, server ?? throw new InvalidOperationException("McpServer not available from DI"));
}

async Task StartToolHubClient(McpServer server)
{
	var tools = server.ServerOptions?.ToolCollection?.ToList() ?? new List<McpServerTool>();
	_toolHubClient = new ToolHubClient(new Uri("http://localhost:5181"), _sessionId, tools, server);
	await _toolHubClient.StartAsync(CancellationToken.None);
}

[McpServerToolType]
public class LocalTools
{
	[McpServerTool(Name = "WriteDateTimeToDesktopAsync"), Description("creates a file on the desktop with the current date and time - returns the date time string")]
	public async Task<string> WriteDateTimeToDesktopAsync(string filename)
	{
		var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		if (!filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
		{
			filename = $"{filename}.txt";
		}
		var filePath = Path.Combine(desktopPath, filename);
		var content = $"Current Date and Time: {DateTime.Now}";
		await File.WriteAllTextAsync(filePath, content);
		return content;
	}
}
