using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using Pisstaube.Crawler;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Engine;
using Pisstaube.Utils;
using Sentry;

namespace Pisstaube.Online.Crawler
{
    /// <summary>
    /// an Crawler that Crawls down osu!API V2
    /// </summary>
    public class OsuCrawler : ICrawler, IDisposable
    {
        private readonly Storage _storage;
        private readonly BeatmapDownloader _beatmapDownloader;
        private object DbContextMutex { get; } = new object();
        private PisstaubeDbContext DbContext { get; }
        private IBeatmapSearchEngineProvider SearchEngine { get; }
        private IAPIProvider ApiProvider { get; }
        private RequestLimiter RequestLimiter { get; }
        
        public int LatestId { get; private set; }
        public bool IsCrawling { get; private set; }
        
        private Thread _workingThread;
        private CancellationTokenSource _cancellationTokenSource;
        
        protected List<Task> Tasks { get; } = new List<Task>();
        protected CancellationToken CancellationToken { get; private set; }
        
        public OsuCrawler(Storage storage, RequestLimiter requestLimiter, IAPIProvider apiProvider,
            IBeatmapSearchEngineProvider searchEngine, BeatmapDownloader beatmapDownloader)
        {
            _storage = storage;
            _beatmapDownloader = beatmapDownloader;
            DbContext = new PisstaubeDbContext();
            SearchEngine = searchEngine;
            ApiProvider = apiProvider;
            RequestLimiter = requestLimiter;

            LatestId = DbContext.BeatmapSet
                           .OrderByDescending(bs => bs.SetId)
                           .Take(1)
                           .ToList()
                           .FirstOrDefault()?.SetId + 1 ?? 1;
        }

        public void Start()
        {
            if (_workingThread?.IsAlive == true)
                return;

            _workingThread = new Thread(ThreadWorker);
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = _cancellationTokenSource.Token;
            
            _workingThread.Start();
        }

        public void Stop()
        {
            if (_workingThread == null)
                throw new NotSupportedException($"It's not possible to Stop {nameof(OsuCrawler)} before it has even Started!");
            
            _cancellationTokenSource.Cancel();
            _workingThread.Join();
        }

        public void Wait()
        {
            _workingThread.Join();
        }

        private int _errorCount;
        public virtual async Task<bool> Crawl(int id)
        {
            Logger.LogPrint($"Crawling BeatmapId {LatestId}...", LoggingTarget.Network, LogLevel.Debug);
            
            try
            {
                var beatmapSetRequest = new GetBeatmapSetRequest(id);
                beatmapSetRequest.Perform(ApiProvider);

                var beatmapSetInfo = beatmapSetRequest.Result;

                if (beatmapSetInfo == null)
                    return false;

                var beatmapSet = BeatmapSet.FromBeatmapSetInfo(beatmapSetInfo.ToBeatmapSet());

                if (beatmapSet == null)
                    return false;

                var cacheStorage = _storage.GetStorageForDirectory("cache");
                
                if (cacheStorage.Exists(beatmapSet.SetId.ToString("x8") + "_novid"))
                    cacheStorage.Delete(beatmapSet.SetId.ToString("x8") + "_novid");
                if (cacheStorage.Exists(beatmapSet.SetId.ToString("x8")))
                    cacheStorage.Delete(beatmapSet.SetId.ToString("x8"));

                lock (DbContextMutex)
                {
                    foreach (var childrenBeatmap in beatmapSet.ChildrenBeatmaps)
                    {
                        var fileInfo = _beatmapDownloader.Download(childrenBeatmap);

                        childrenBeatmap.FileMd5 = fileInfo.Item2;
                    }
                    
                    beatmapSet.LastChecked = new DateTime();
                    
                    DbContext.BeatmapSet.AddOrUpdate(beatmapSet);
  
                    DbContext.SaveChanges();
                }

                SearchEngine.Index(new []{ beatmapSet });

                _errorCount = 0;
                return true;
            }
            catch (WebException) // Don't worry about WebException exceptions, we can safely ignore those.
            {
                _errorCount++;
                return false;
            } 
            catch (Exception e) // Everything else, redo the Crawl.
            {
                DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                Logger.Error(e, "Unknown error during crawling occured!");

                SentrySdk.CaptureException(e);

                if (_errorCount > 1024)
                {
                    Logger.LogPrint("Error count too high! canceling crawl...", LoggingTarget.Network, LogLevel.Important);
                    return false;
                }

                _errorCount++;
                return await Crawl(id);
            }
        }
        
        protected virtual void ThreadWorker()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                if (_errorCount > 1024)
                {
                    Logger.LogPrint("Error count too high! will continue tomorrow...", LoggingTarget.Network, LogLevel.Important);
                    Thread.Sleep(TimeSpan.FromDays(1));
                }
                if (Tasks.Count > 32) {
                    foreach (var task in Tasks) // wait for all tasks
                    {
                        task.Wait(CancellationToken);
                    }
                    
                    Tasks.Clear(); // Remove all previous tasks.
                }
                
                RequestLimiter.Limit();

                Tasks.Add(Crawl(LatestId++));
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            DbContext?.Dispose();
        }
    }
}