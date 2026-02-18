using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;


namespace MCPSample.Tools
{
    public class MetaModelStore
    {
        [McpServerResource(UriTemplate = "file://Metamodel/{kind}.json")]
        [Description("SCL model metadata, containing object properties and hierarchy for a given document kind")]
        public static string ReadMetaModel([Description("Document kind")] string kind)
        {
            var path = $"./Metamodel/{kind}.json";
            if (File.Exists(path))
                return File.ReadAllText(path);
            throw new Exception("Can't read " + path);
        }
           
    }
}