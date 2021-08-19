using Serilog;
using System;

namespace PrettySus.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            using var app = new ServerApp(args);
            app.Run();
        }
    }
}
