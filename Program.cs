using Microsoft.Extensions.AI;
using OllamaSharp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

// IChatClient chatClient =
//     new OllamaApiClient(new Uri("http://localhost:11434/"), "phi3:mini");
IChatClient chatClient = new ChatClientBuilder(

    new OllamaApiClient(new Uri("http://localhost:11434/"), "gpt-oss:20b"))
    .UseFunctionInvocation()
    .UseLogging()
    .Build(new MCPSample.P1());

// TODO use service providing ILoggerFactory

// Create the MCP client
// Configure it to start and connect to your MCP server.
McpClient mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "dotnet",
        Arguments = ["../MCPServer/bin/Debug/net8.0/linux-x64/MCPServer.dll"],
        Name = "Minimal MCP Server",
    }));

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"Connected to server with tools: {tool.Name}");
}

var options = new ChatOptions 
{ 
    MaxOutputTokens = 1000, 
    AllowMultipleToolCalls = true, 
    ModelId = "gpt-oss:20b",  
    Tools = [..tools]
};
    

// Start the conversation with context for the AI model
List<ChatMessage> chatHistory = new();

chatHistory.Add(new ChatMessage(ChatRole.System, "You are testing mcp servers. Use MCP server when applicable."
+ "\n\t - Use MCP server for getting random numbers."
+ "\n\t - Use MCP server for secret treasure keys."
));

int cnt = 0;
bool continueProcess = true;
while (continueProcess)
{
    if (cnt == 0)
    {
        cnt++;
    }
    else
    {
        // Get user prompt and add to chat history
        Console.WriteLine("Your prompt:");
        var userPrompt = Console.ReadLine();
        if (userPrompt is null || userPrompt.ToLower() == "end" || userPrompt.ToLower() == "quit")
        {
            continueProcess = false;
        }
        chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));
    }
    if (!continueProcess)
    {
        await mcpClient.DisposeAsync();
        break;
    }

    // Stream the AI response and add to chat history
    Console.WriteLine("AI Response:");
    var response = "";
    await foreach (ChatResponseUpdate item in
        chatClient.GetStreamingResponseAsync(chatHistory, options))
    {
        Console.Write(item.Text);
        response += item.Text;
    }
    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    Console.WriteLine();
}