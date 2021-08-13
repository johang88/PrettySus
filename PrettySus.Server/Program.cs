using System;

namespace PrettySus.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            using var app = new ServerApp();
            app.Run();
        }
    }
}
