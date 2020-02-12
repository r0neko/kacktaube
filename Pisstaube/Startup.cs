using System;
using System.Threading;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Online.API;
using Pisstaube.Allocation;
using Pisstaube.CacheDb;
using Pisstaube.Crawler;
using Pisstaube.Database;
using Pisstaube.Engine;
using Pisstaube.Online;
using Pisstaube.Online.Crawler;
using Pisstaube.Utils;

namespace Pisstaube
{
    public class Startup
    {
        private readonly Storage dataStorage = new NativeStorage("data");
        private readonly DatabaseContextFactory _osuContextFactory;
        
        public ILifetimeScope AutofacContainer { get; private set; }
        
        // ReSharper disable once UnusedParameter.Local
        public Startup(IConfiguration configuration)
        {
            _osuContextFactory = new DatabaseContextFactory(dataStorage);
        }
        
        
        private readonly PisstaubeCacheDbContextFactory _cacheContextFactory;

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddRouting();
            services.AddHealthChecks();
            
            services
                .AddMvc(
                    options =>
                    {
                        options.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                        options.EnableEndpointRouting = false;
                    })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            
            services.AddRouting();
            
            var builder = new ContainerBuilder();
            
            builder.Populate(services);
            builder.RegisterType<OsuCrawler>().AsSelf().As<ICrawler>().SingleInstance();
            builder.RegisterType<DatabaseHouseKeeper>().SingleInstance();
            
            builder.RegisterType<Cache>().SingleInstance();
            builder.RegisterType<IpfsCache>().SingleInstance();
            
            builder.RegisterInstance(new RequestLimiter(1200, TimeSpan.FromMinutes(1)));
            builder.RegisterInstance(dataStorage).As<Storage>();
            builder.RegisterInstance(_osuContextFactory).As<IDatabaseContextFactory>();
            builder.RegisterType<FileStore>();
            builder.RegisterType<PisstaubeDbContext>().InstancePerDependency();
            builder.RegisterType<PisstaubeCacheDbContextFactory>().AsSelf();
            builder.RegisterType<SetDownloader>().AsSelf();
            builder.RegisterType<SmartStorage>().AsSelf();

            builder.RegisterType<BeatmapSearchEngine>().As<IBeatmapSearchEngineProvider>();
            builder.RegisterType<BeatmapDownloader>();

            builder.RegisterType<OsuConfigManager>();
            builder.RegisterType<APIAccess>().As<IAPIProvider>().SingleInstance();

            AutofacContainer = builder.Build();
            
            return new AutofacServiceProvider(AutofacContainer);
        }


        [UsedImplicitly]
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env,
            ICrawler crawler, IAPIProvider apiProvider, DatabaseHouseKeeper houseKeeper,
            PisstaubeCacheDbContextFactory cacheDbContextFactory)
        {
            cacheDbContextFactory.Get().Migrate();
            _osuContextFactory.Get().Migrate();

            Logger.Enabled = true;
            Logger.Level = LogLevel.Debug;
            Logger.GameIdentifier = "Pisstaube";
            Logger.Storage = dataStorage.GetStorageForDirectory("logs");
            
            JsonUtil.Initialize();

            apiProvider.Login(Environment.GetEnvironmentVariable("OSU_USERNAME"),
                Environment.GetEnvironmentVariable("OSU_PASSWORD"));

            GlobalConfig.EnableCrawling = Environment.GetEnvironmentVariable("CRAWLER_DISABLED")?.ToLowerInvariant() == "false";
            GlobalConfig.EnableUpdating = Environment.GetEnvironmentVariable("UPDATER_DISABLED")?.ToLowerInvariant() == "false";
            
            while (true)
            {
                if (!apiProvider.IsLoggedIn)
                {
                    Logger.LogPrint("Not Logged in yet...");
                    Thread.Sleep(1000);
                    //continue;
                }
                if (apiProvider.State == APIState.Failing)
                {   
                    Logger.LogPrint($"Failed to Login using Username {Environment.GetEnvironmentVariable("OSU_USERNAME")}", LoggingTarget.Network, LogLevel.Error);
                    Environment.Exit(1);
                }

                break;
            }

            if (DebugUtils.IsDebugBuild)
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();
            
            if (GlobalConfig.EnableCrawling)
                crawler.Start();
            
            if (GlobalConfig.EnableUpdating)
                houseKeeper.Start();

            app.UseMvc(routes => routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}"));
        }
    }
}