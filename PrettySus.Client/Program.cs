using System;

namespace PrettySus.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            using var app = new ClientApp();
            app.Run();
        }
    }
}
