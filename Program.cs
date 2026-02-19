using Microsoft.Extensions.AI;
using OllamaSharp;
using System.Diagnostics;
using System.Text.Json;
using System.Numerics.Tensors;
using System.Text.Json.Nodes; // Для расчета схожести
using System.Linq;

string llmId = "gpt-oss:20b";
string embeddingModel = "embeddinggemma:300m";
// "yandex/YandexGPT-5-Lite-8B-instruct-GGUF:latest"; // TODO should be configurable
// "gpt-oss:20b"

var ollamaClient = new OllamaApiClient(new Uri("http://localhost:11434/"), llmId);
// IChatClient chatClient =
//     new OllamaApiClient(new Uri("http://localhost:11434/"), "phi3:mini");
IChatClient chatClient = new ChatClientBuilder(ollamaClient)
    .UseFunctionInvocation()
    .UseLogging()
    .Build(new MCPSample.P1());

// Load and process metamodel annotations for RAG
Debug.Assert(File.Exists("./Metamodel/ssd.json"));
JsonDocument? doc = null;
using (var stm = File.OpenRead("./Metamodel/ssd.json"))
{
    doc = JsonDocument.Parse(stm);
}
var annotations = doc.RootElement.GetProperty("Annotations");

// Create embeddings for each annotation
var annotationEmbeddings = new List<(string name, string title, float[] embedding)>();

foreach (var annotation in annotations.EnumerateArray())
{
    var name = annotation.GetProperty("Name").GetString() ?? "";
    var title = annotation.GetProperty("Title").GetString() ?? "";
    var text = $"Name: {name} Название: {title}";

    var response = await ollamaClient.EmbedAsync(new OllamaSharp.Models.EmbedRequest()
    {

        Model = embeddingModel,
        Input = [text]
    });
    
    // Get embedding from Ollama
    // var embeddingResponse = await ollamaClient.Embeddings.Create();
    var embedding = response.Embeddings[0];
    
    annotationEmbeddings.Add((name, title, embedding));
}

// Function to calculate cosine similarity
static float CosineSimilarity(float[] a, float[] b)
{
    if (a.Length != b.Length) throw new ArgumentException("Vectors must have the same length");
    
    float dotProduct = 0;
    float normA = 0;
    float normB = 0;
    
    for (int i = 0; i < a.Length; i++)
    {
        dotProduct += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    
    return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
}

// Function to find most relevant annotation
async Task<(string name, string title, float similarity)> FindMostRelevantAnnotation(string prompt)
{
    // Get embedding for the prompt
    var promptEmbeddingResponse = await ollamaClient.EmbedAsync(new OllamaSharp.Models.EmbedRequest()
    {
        Model = embeddingModel,
        Input = [prompt]
    });
    var promptEmbedding = promptEmbeddingResponse.Embeddings[0];
    
    // Find the most similar annotation
    var bestMatch = annotationEmbeddings
        .Select(x => (x.name, x.title, similarity: CosineSimilarity(x.embedding, promptEmbedding)))
        .OrderByDescending(x => x.similarity)
        .First();
    
    return bestMatch;
}

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
    ModelId = ollamaClient.SelectedModel,
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
        
        // Find most relevant annotation and add to context
        if (!string.IsNullOrEmpty(userPrompt))
        {
            var (name, title, similarity) = await FindMostRelevantAnnotation(userPrompt);
            if (similarity > 0.4f) // Threshold for relevance
            {
                // Find the full annotation JSON
                var relevantAnnotation = annotations.EnumerateArray()
                    .FirstOrDefault(a => a.GetProperty("Name").GetString() == name);
                
                if (relevantAnnotation.ValueKind != JsonValueKind.Undefined)
                {
                    var annotationJson = relevantAnnotation.ToString();
                    userPrompt += $"\n\nRelevant metamodel context:\n{annotationJson}";
                }
            }
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