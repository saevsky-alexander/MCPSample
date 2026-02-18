using System.ComponentModel;
using ModelContextProtocol.Server;


/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
internal class RandomNumberTools
{
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public static int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        return Random.Shared.Next(min, max);
    }

    [Description("Tells a Secret Treasure key for a human")]
    public static int GetSecretKey([Description("Human name")] string name)
        => name switch
        {
            "Danny" => 1771,
            "Jimmy" => 34567,
            "Colonell Assad" => 333,
            "Serge Brin" => 991,
            _ => 0
        };

    [Description("Get a metamodel for a document")]
    public static string GetMetamodel([Description("Document kind")] string kind)
    {
            var path = $"./Metamodel/{kind}.json";
            if (File.Exists(path))
                return File.ReadAllText(path);
            throw new Exception("Can't read " + path);
    }
}
