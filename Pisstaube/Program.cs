using System;
using System.IO;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using dotenv.net;
using osu.Framework.Logging;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace Pisstaube
{
    public class Program
    {
        private static readonly CancellationTokenSource Cts = new CancellationTokenSource();

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += OnProcessExit;
            if (!File.Exists(".env"))
            {
                if (File.Exists(".env"))
                    goto SKIP; // Assume this is a docker environment!

                File.Copy(".env.example", ".env");
                Console.WriteLine("Config has been created! please edit");
                return;
            }

            DotEnv.Config();
            SKIP:
            CreateWebHostBuilder(args).Build().RunAsync(Cts.Token).GetAwaiter().GetResult();
        }

        private static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseSentry(Environment.GetEnvironmentVariable("SENTRY_DNS"))
                .UseKestrel(opt =>
                {
                    opt.Limits.MaxRequestBodySize = null;
                    opt.Listen(IPAddress.Any, 5000);
                })
                .UseShutdownTimeout(TimeSpan.FromSeconds(5))
                .UseStartup<Startup>();

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Logger.LogPrint("Killing everything..", LoggingTarget.Information, LogLevel.Important);
            Cts.Cancel();
        }
    }
}