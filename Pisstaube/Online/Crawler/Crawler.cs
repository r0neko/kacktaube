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
        private bool _should_stop;
        private bool _force_stop;
        private int _fail_count;

        public int LatestId { get; private set; }
        public bool IsCrawling { get; private set; }

        private object _lock = new object ( );
        private List<Thread> _pool;

        private readonly BeatmapSearchEngine _search;
        private readonly APIAccess _apiAccess;
        private readonly RulesetStore _store;
        private readonly BeatmapDownloader _downloader;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly RequestLimiter _rl;
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly int _workerThreads;
        private Thread _thread_restarter;

        public Crawler (BeatmapSearchEngine search,
            APIAccess apiAccess,
            RulesetStore store,
            BeatmapDownloader downloader,
            PisstaubeCacheDbContextFactory cache,
            RequestLimiter rl,
            PisstaubeDbContextFactory contextFactory)
        {
            _pool = new List<Thread> ( );
            _search = search;
            _apiAccess = apiAccess;
            _store = store;
            _downloader = downloader;
            _cache = cache;
            _rl = rl;
            _contextFactory = contextFactory;
            _workerThreads = int.Parse (Environment.GetEnvironmentVariable ("CRAWLER_THREADS") ??
                throw new Exception ("CRAWLER_THREADS MUST be an Int!"));
        }

        public void BeginCrawling ( )
        {
            Logger.LogPrint ("Begin Crawling!");
            _force_stop = false;
            _should_stop = false;
            IsCrawling = true;

            while (true)
            {
                if (_thread_restarter == null)
                    _thread_restarter = new Thread (( ) =>
                    {
                        Logger.LogPrint ("Thread Restarter begun it's work!");
                        _should_stop = false;

                        while (!_force_stop)
                        {
                            for (var i = 0; i < _workerThreads; i++)
                            {
                                _pool.Add (new Thread (_crawl));

                                _pool.Last ( ).Start ( );
                            }

                            while (!_should_stop)
                            {
                                Thread.Sleep (50);
                                if (_force_stop) break;
                            }

                            if (_force_stop) break;

                            Logger.LogPrint ("Crawler finished! Gonna try again in 1d", LoggingTarget.Information);
                            Thread.Sleep (TimeSpan.FromDays (1));
                        }
                    });
                else
                {
                    _thread_restarter = null;
                    continue;
                }

                if (_force_stop)
                {
                    IsCrawling = false;
                    break;
                }

                _thread_restarter.Start ( );
                break;
            }
        }

        public void Stop ( )
        {
            Logger.LogPrint ("Stop Crawling");
            _force_stop = true;
            _should_stop = true;
            Wait ( );

            _pool = new List<Thread> ( );

            LatestId = 0;
        }

        public void Wait ( )
        {
            foreach (var thread in _pool)
                thread.Join ( );
        }

        private void _crawl ( )
        {
            lock (_lock)
            if (LatestId == 0)
                LatestId = _contextFactory.Get ( ).BeatmapSet.LastOrDefault ( )?.SetId + 1 ?? 0;

            while (!_should_stop)
            {
                int id;
                lock (_lock)
                id = LatestId++;

                // ReSharper disable once InconsistentlySynchronizedField
                using (var db = _contextFactory.GetForWrite ( ))
                {
                    if (!Crawl (id, db.Context))
                        _fail_count++;
                    else
                        _fail_count = 0;
                }

                if (_fail_count > 1000) // We failed 1000 times, lets try tomorrow again! maybe there are new exciting beatmaps!
                    _should_stop = true;

                IsCrawling = !_should_stop;
            }
        }

        public bool Crawl (int id, PisstaubeDbContext _context)
        {
            try
            {
                while (!_apiAccess.IsLoggedIn)
                    Thread.Sleep (1000);

                var setRequest = new GetBeatmapSetRequest (id);

                _rl.Limit ( );
                setRequest.Perform (_apiAccess);

                var apiSet = setRequest.Result;

                var localSet = apiSet?.ToBeatmapSet (_store);
                if (localSet == null)
                    return false;

                var dbSet = BeatmapSet.FromBeatmapSetInfo (localSet);
                if (dbSet == null)
                    return false;

                foreach (var map in dbSet.ChildrenBeatmaps)
                {
                    var fileInfo = _downloader.Download (map);

                    map.FileMd5 = _cache.Get ( )
                        .CacheBeatmaps
                        .Where (cmap => cmap.Hash == fileInfo.Hash)
                        .Select (cmap => cmap.FileMd5)
                        .FirstOrDefault ( );
                }

                _context.BeatmapSet.Add (dbSet);
                _search.IndexBeatmap (dbSet);
            }
            catch (Exception ex)
            {
                Logger.Error (ex, $"Unknown Error occured while crawling Id {id}!");

                lock (_lock)
                Thread.Sleep (TimeSpan.FromMinutes (1));
                return false;
            }

            return true;
        }
    }
}
