using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Engine;
using StatsdClient;

namespace Pisstaube.Online.Crawler
{
    public class Kaesereibe : ICrawler
    {
        private bool _shouldStop;
        private bool _forceStop;
        private int _failCount;

        public int LatestId { get; private set; }
        public bool IsCrawling { get; private set; }

        private object _lock = new object();
        private List<Thread> _pool;

        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly BeatmapSearchEngine _searchEngine;
        private readonly int _workerThreads;
        private Thread _threadRestarter;

        public Kaesereibe(PisstaubeDbContextFactory contextFactory, BeatmapSearchEngine searchEngine)
        {
            _pool = new List<Thread>();
            _contextFactory = contextFactory;
            _searchEngine = searchEngine;
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
                            DogStatsd.ServiceCheck("crawler.is_crawling", Status.OK);
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

                            DogStatsd.ServiceCheck("crawler.is_crawling", Status.CRITICAL);
                            Logger.LogPrint("Crawler finished! Gonna retry again in 1d", LoggingTarget.Information);
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

                if (_failCount > 1000)
                    _shouldStop = true;
            }
        }

        public bool Crawl(int id, PisstaubeDbContext context)
        {
            try
            {
                var setRequest =
                    new JsonWebRequest<BeatmapSet>(
                        $"https://{Environment.GetEnvironmentVariable("CHEESEGULL_API")}/api/s/{id}");

                try
                {
                    setRequest.Perform();
                }
                catch
                {
                    if (!setRequest.ResponseString.StartsWith("null"))
                        throw;
                }

                var apiSet = setRequest.ResponseObject;
                if (apiSet == null)
                    return false;

                _searchEngine.IndexBeatmap(apiSet);
                context.BeatmapSet.Add(apiSet);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unknown Error occured while crawling Id {id}!");

                // lock (_lock)
                // Thread.Sleep(TimeSpan.FromMinutes(1));
                return false;
            }

            return true;
        }
    }
}