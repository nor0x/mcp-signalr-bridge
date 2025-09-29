# MCP SignalR Bridge

A demonstration of connecting a locally running MCP (Model Context Protocol) Server to a hosted LLM API using SignalR for real-time communication.

## Overview

This project shows how to bridge the gap between local MCP servers and cloud-hosted LLM APIs, enabling LLMs to interact with local system resources while maintaining the benefits of hosted AI services.

## Try It Out
1. Clone the repository.
2. Update the API key in `API/Program.cs` with your OpenAI API key.
3. Build and run the API project:
   ```bash
   cd API
   dotnet run

   cd Client
   dotnet run

   # prompt something like:
   > Write a text file on my desktop named "datetime.txt" containing the current date and time.
   ```

## Technologies Used

- .NET 10
- ASP.NET Core
- SignalR
- Microsoft.Extensions.AI
- OpenAI

## Architecture

```mermaid
graph TB
    subgraph "Cloud Environment"
        User[User] --> API[ASP.NET Core API]
        API --> LLM[LLM Provider<br/>OpenAI ChatClient]
        API --> Broker[ClientToolBroker]
        API --> Hub[SignalR Hub<br/>ToolHub]
        
        LLM --> |Function Call| Proxy[Proxy Tool Method<br/>WriteDateTimeToDesktopAsync]
        Proxy --> Broker
        Broker --> Hub
    end
    
    subgraph "Local Environment"
        subgraph "MCP Host Application"
            HubClient[SignalR Hub Client<br/>ToolHubClient]
            MCPClient[MCP Client]
            MCPServer[MCP Server]
            LocalTools[Local Tools<br/>WriteDateTimeToDesktopAsync]
        end
        
        Desktop[Desktop File System]
    end
    
    %% Cross-environment communication
    Hub <--> |SignalR WebSocket| HubClient
    
    %% Local environment flows
    HubClient --> |Tool Call| MCPClient
    MCPClient --> |clientPipe| MCPServer
    MCPServer --> |serverPipe| MCPClient
    MCPServer --> |Execute| LocalTools
    LocalTools --> |Write File| Desktop
    
    %% Data flow annotations
    Hub --> |ToolCallEnvelope| HubClient
    HubClient --> |ToolResultEnvelope| Hub
    
    %% Styling with more vibrant colors
    classDef cloud fill:#1565c0,stroke:#0d47a1,stroke-width:3px,color:#ffffff
    classDef local fill:#2e7d32,stroke:#1b5e20,stroke-width:3px,color:#ffffff
    classDef mcp fill:#f57c00,stroke:#e65100,stroke-width:3px,color:#ffffff
    classDef user fill:#d32f2f,stroke:#b71c1c,stroke-width:3px,color:#ffffff
    classDef desktop fill:#7b1fa2,stroke:#4a148c,stroke-width:3px,color:#ffffff
    
    class API,LLM,Hub,Broker,Proxy cloud
    class HubClient,MCPClient,MCPServer,LocalTools local
    class Desktop desktop
    class User user
```


## Security Considerations

⚠️ **Warning**: This is a proof-of-concept. In production:
- Implement proper authentication and authorization
- Add LLM guardrails and tool call supervision
- Validate all inputs and sanitize file operations
- Consider using secure tunneling instead of direct SignalR connections

## Blog Post

Read the full technical breakdown: [Connecting a local MCP Server to a hosted LLM API](https://johnnys.news/2025/09/Connecting-a-local-MCP-Server-to-a-hosted-LLM-API/)

## License
MIT License. See `LICENSE` file for details.
