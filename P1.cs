using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MCPSample
{
class P1 : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        if (serviceType.Equals(typeof(ILoggerFactory)))
        return LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            // Optional: Configure log levels, for example:
            // builder.SetMinimumLevel(LogLevel.Debug); 
        });
        return null;
    }
}
}