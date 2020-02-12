using System;
using System.IO;
using Autofac.Extensions.DependencyInjection;
using dotenv.net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace Pisstaube
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("IS_CONTAINER") != "true")
                DotEnv.Config();
            
            if (!Directory.Exists("./data"))
                Directory.CreateDirectory("data");

            var host = WebHost.CreateDefaultBuilder(args)
                .UseKestrel()
                .ConfigureServices(services => services.AddAutofac())
                .UseContentRoot(Path.Join(Directory.GetCurrentDirectory(), "data"))
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}