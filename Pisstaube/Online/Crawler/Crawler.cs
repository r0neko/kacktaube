using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Engine;
using Pisstaube.Utils;

namespace Pisstaube.Online.Crawler
{
    public class Crawler : ICrawler
    {
        private bool _shouldStop;
        private bool _forceStop;
        private int _failCount;

        public int LatestId { get; private set; } = 0;
        public bool IsCrawling { get; private set; } = false;

        private object _lock = new object();
        private List<Thread> _pool;

        private readonly BeatmapSearchEngine _search;
        private readonly APIAccess _apiAccess;
        private readonly RulesetStore _store;
        private readonly BeatmapDownloader _downloader;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly RequestLimiter _rl;
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly int _workerThreads;
        private Thread _threadRestarter;

        public Crawler(BeatmapSearchEngine search,
            APIAccess apiAccess,
            RulesetStore store,
            BeatmapDownloader downloader,
            PisstaubeCacheDbContextFactory cache,
            RequestLimiter rl,
            PisstaubeDbContextFactory contextFactory)
        {
            _pool = new List<Thread>();
            _search = search;
            _apiAccess = apiAccess;
            _store = store;
            _downloader = downloader;
            _cache = cache;
            _rl = rl;
            _contextFactory = contextFactory;
            _workerThreads = int.Parse(Environment.GetEnvironmentVariable("CRAWLER_THREADS") ??
                                       throw new Exception("CRAWLER_THREADS MUST be an Int!"));
        }

        public void BeginCrawling()
        {
            Logger.LogPrint("Begin Crawling!");
            _forceStop = false;
            _shouldStop = false;
            IsCrawling = true;

            while (true)
            {
                if (_threadRestarter == null)
                {
                    _threadRestarter = new Thread(() =>
                    {
                        Logger.LogPrint("Thread Restarter begun it's work!");
                        _shouldStop = false;

                        while (!_forceStop)
                        {
                            for (var i = 0; i < _workerThreads; i++)
                            {
                                _pool.Add(new Thread(_crawl));

                                _pool.Last().Start();
                            }

                            while (!_shouldStop)
                            {
                                Thread.Sleep(50);
                                if (_forceStop) break;
                            }

                            if (_forceStop) break;

                            Logger.LogPrint("Crawler finished! Gonna try again in 1d", LoggingTarget.Information);
                            Thread.Sleep(TimeSpan.FromDays(1));
                        }
                    });
                }
                else
                {
                    _threadRestarter = null;
                    continue;
                }

                if (_forceStop)
                {
                    IsCrawling = false;
                    break;
                }

                _threadRestarter.Start();
                break;
            }
        }

        public void Stop()
        {
            Logger.LogPrint("Stop Crawling");
            _forceStop = true;
            _shouldStop = true;
            Wait();

            _pool = new List<Thread>();

            LatestId = 0;
        }

        public void Wait()
        {
            foreach (var thread in _pool)
                thread.Join();
        }

        private void _crawl()
        {
            lock (_lock)
                if (LatestId == 0)
                    LatestId = _contextFactory.Get().BeatmapSet.LastOrDefault()?.SetId + 1 ?? 0;

            while (!_shouldStop)
            {
                int id;
                lock (_lock)
                    id = LatestId++;

                // ReSharper disable once InconsistentlySynchronizedField
                using (var db = _contextFactory.GetForWrite())
                    if (!Crawl(id, db.Context))
                        _failCount++;
                    else
                        _failCount = 0;

                if (_failCount > 1000
                ) // We failed 1000 times, lets try tomorrow again! maybe there are new exciting beatmaps!
                    _shouldStop = true;

                IsCrawling = !_shouldStop;
            }
        }

        public bool Crawl(int id, PisstaubeDbContext context)
        {
            try
            {
                while (!_apiAccess.IsLoggedIn)
                    Thread.Sleep(1000);

                var setRequest = new GetBeatmapSetRequest(id);

                _rl.Limit();
                setRequest.Perform(_apiAccess);

                var apiSet = setRequest.Result;

                var localSet = apiSet?.ToBeatmapSet(_store);
                if (localSet == null)
                    return false;

                var dbSet = BeatmapSet.FromBeatmapSetInfo(localSet);
                if (dbSet == null)
                    return false;

                foreach (var map in dbSet.ChildrenBeatmaps)
                {
                    var fileInfo = _downloader.Download(map);

                    map.FileMd5 = _cache.Get()
                        .CacheBeatmaps
                        .Where(cmap => cmap.Hash == fileInfo.Hash)
                        .Select(cmap => cmap.FileMd5)
                        .FirstOrDefault();
                }

                context.BeatmapSet.Add(dbSet);
                _search.IndexBeatmap(dbSet);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unknown Error occured while crawling Id {id}!");

                lock (_lock)
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                return false;
            }

            return true;
        }
    }
}