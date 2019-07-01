using System;
using System.Threading;
using dotenv.net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using osu.Framework.Logging;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace Pisstaube
{
    public class Program
    {
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();
        
        public static void Main(string[] args)
        {
            Console.CancelKeyPress += OnProcessExit;
            DotEnv.Config();
            CreateWebHostBuilder(args).Build().RunAsync(cts.Token).GetAwaiter().GetResult();
        }

        private static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseSentry(Environment.GetEnvironmentVariable("SENTRY_DNS"))
                .UseKestrel(opt => opt.Limits.MaxRequestBodySize = null)
                .UseShutdownTimeout(TimeSpan.FromSeconds(5))
                .UseStartup<Startup>();
        
        private static void OnProcessExit(object sender, EventArgs e)
        {
            Logger.LogPrint("Killing everything..", LoggingTarget.Information, LogLevel.Important);
            cts.Cancel();
        }
    }
}