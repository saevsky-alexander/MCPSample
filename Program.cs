using Microsoft.Extensions.AI;
using OllamaSharp;
using System.Diagnostics;
using System.Text.Json;
using System.Numerics.Tensors;
using System.Text.Json.Nodes; // Для расчета схожести

string llmId = "yandex/YandexGPT-5-Lite-8B-instruct-GGUF:latest"; // TODO should be configurable

var ollamaClient = new OllamaApiClient(new Uri("http://localhost:11434/"), llmId);
// IChatClient chatClient =
//     new OllamaApiClient(new Uri("http://localhost:11434/"), "phi3:mini");
IChatClient chatClient = new ChatClientBuilder(ollamaClient)
    .UseFunctionInvocation()
    .UseLogging()
    .Build(new MCPSample.P1());

Debug.Assert(File.Exists("./Metamodel/ssd.json"));
JsonDocument? doc = null;
using (var stm = File.OpenRead("./Metamodel/ssd.json"))
{
    doc = JsonDocument.Parse(stm);
}
var ann = doc.RootElement.GetProperty("Annotations");
// foreach (var item in (JsonArray) ann)


// TODO use service providing ILoggerFactory

// Create the MCP client
// Configure it to start and connect to your MCP server.
#if false
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
#endif

var options = new ChatOptions 
{ 
    MaxOutputTokens = 1000, 
    AllowMultipleToolCalls = true, 
    ModelId = "gpt-oss:20b",  
    Instructions = File.ReadAllText("MetamodelInstruction.md"),
    // Tools = [..tools]
    Tools = [
        AIFunctionFactory.Create(RandomNumberTools.GetRandomNumber, "get_random_number"),
        AIFunctionFactory.Create(RandomNumberTools.GetRandomNumber, "get_random_number"),
        AIFunctionFactory.Create(RandomNumberTools.GetSecretKey),
        AIFunctionFactory.Create(RandomNumberTools.GetMetamodel, "get-metamodel")
    ],
    

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
        if (userPrompt is null 
        || userPrompt.Equals("end", StringComparison.CurrentCultureIgnoreCase) 
        || userPrompt.Equals("quit", StringComparison.CurrentCultureIgnoreCase))
        {
            continueProcess = false;
        }
        chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));
    }
    if (!continueProcess)
    {
        // await mcpClient.DisposeAsync();
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