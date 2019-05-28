using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using opi.v1;
using osu.Framework.Logging;
using osu.Game.Online.API.Requests;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using StatsdClient;

namespace Pisstaube.Utils
{
    public class Crawler
    {
        private bool _should_stop;
        private bool _force_stop;
        private int _latest_Id;
        private int _fail_count;
        private int _request_count;
        
        private object _lock = new object();
        private List<Thread> _pool;
        
        private readonly Api _api;
        private readonly BeatmapSearchEngine _search;
        private readonly int _workerThreads;
        private Thread _thread_restarter;
        private Thread _dd_thread;

        public Crawler(BeatmapSearchEngine search)
        {
            _pool = new List<Thread>();
            _search = search;
            _workerThreads = int.Parse(Environment.GetEnvironmentVariable("CRAWLER_THREADS"));
            _api = new Api(Environment.GetEnvironmentVariable("OSU_API_KEY"));
        }

        public void BeginCrawling()
        {
            Logger.LogPrint("Begin Crawling!");
            _force_stop = false;
            _should_stop = false;

            if (_dd_thread != null) {
                _dd_thread = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        lock (_lock) DogStatsd.Set("crawler.latest_id", _latest_Id);
                    }
                });
                _dd_thread.Start();
            }

            while (true)
            {
                if (_thread_restarter == null)
                    _thread_restarter = new Thread(() =>
                    {
                        Logger.LogPrint("Thread Restarter begun it's work!");
                        _should_stop = false;

                        while (!_force_stop)
                        {
                            DogStatsd.ServiceCheck("crawler.is_crawling", Status.OK);
                            for (var i = 0; i < _workerThreads; i++)
                            {
                                _pool.Add(new Thread(_crawl));

                                _pool.Last().Start();
                            }

                            while (!_should_stop)
                            {
                                Thread.Sleep(50);
                                if (_force_stop) break;
                            }
                            
                            if (_force_stop) break;
                            
                            DogStatsd.ServiceCheck("crawler.is_crawling", Status.CRITICAL);
                            Logger.LogPrint("Crawler finished! Gonna retry again in 1d", LoggingTarget.Information);
                            Thread.Sleep(TimeSpan.FromDays(1));
                        }
                    });
                else
                {
                    _thread_restarter.Abort();
                    _thread_restarter = null;
                    continue;
                }

                if (_force_stop) break;

                _thread_restarter.Start();
                break;
            }
        }

        public void Stop()
        {
            Logger.LogPrint("Stop Crawling");
            _force_stop = true;
            _should_stop = true;
        }

        public void Wait()
        {
            _pool.Last().Join();
        }

        private void _crawl()
        {
            var _context = new PisstaubeDbContext();

            lock (_lock)
                if (_latest_Id == 0)
                    _latest_Id = _context.BeatmapSet.LastOrDefault()?.SetId + 1 ?? 0;
            
            while (!_should_stop)
            {
                int id;
                lock (_lock)
                    id = _latest_Id++;

                Crawl(id, _context);

                if (_fail_count > 50) // We failed 50 times, lets try tomorrow again! maybe there are new exciting beatmaps!
                    _should_stop = true;
            }
            
            _context.Dispose();
        }

        public bool Crawl(int id, PisstaubeDbContext _context)
        {
            try
            {
                Logger.LogPrint($"Crawling Id: {id}");

                lock (_lock)
                    _request_count++;

                var maps = _api.GetBeatmapSet(id);
                
                lock (_lock)
                    if (_request_count > int.Parse(Environment.GetEnvironmentVariable("CRAWLER_REQUESTS_PER_MINUTE")))
                        Thread.Sleep(TimeSpan.FromMinutes(1));

                if (maps == null || maps.Length == 0)
                {
                    _fail_count++;
                    return false;
                }

                var ptmap = new BeatmapSet();
                ptmap.ChildrenBeatmaps = new List<ChildrenBeatmap>();
                
                foreach (var map in maps)
                {
                    var pmap = new ChildrenBeatmap();

                    ptmap.Artist = map.Artist;
                    ptmap.Creator = map.Creator;
                    ptmap.Favourites = map.FavouriteCount;
                    ptmap.Genre = map.Genre;
                    ptmap.Language = map.Language;
                    ptmap.Source = string.Empty;
                    ptmap.Tags = map.Tags;
                    ptmap.Title = map.Title;
                    ptmap.ApprovedDate = map.RankedDate;
                    ptmap.HasVideo = false;
                    ptmap.LastChecked = DateTime.Now;
                    ptmap.LastUpdate = map.LastUpdate;
                    ptmap.RankedStatus = map.RankedStatus;
                    ptmap.SetId = map.BeatmapSetId;

                    pmap.BeatmapId = map.BeatmapId;
                    pmap.ParentSetId = map.BeatmapSetId;
                    pmap.DiffName = map.DifficultyName;
                    pmap.FileMd5 = map.BeatmapMd5;
                    pmap.Mode = map.PlayMode;
                    pmap.Bpm = map.Bpm;
                    pmap.Ar = map.Ar;
                    pmap.Od = map.Od;
                    pmap.Cs = map.Cs;
                    pmap.Hp = map.Hp;
                    pmap.TotalLength = map.TotalLength;
                    pmap.HitLength = map.HitLength;
                    pmap.Playcount = map.PlayCount;
                    pmap.Passcount = map.PassCount;
                    pmap.MaxCombo = map.MaxCombo;
                    pmap.DifficultyRating = map.Difficulty;

                    ptmap.ChildrenBeatmaps.Add(pmap);
                }

                lock (_lock)
                {
                    _context.BeatmapSet.Add(ptmap);
                    _context.SaveChanges();
                }

                _search.IndexBeatmap(ptmap);
                _fail_count = 0;
            }
            catch (JsonException)
            {
                Console.WriteLine($"Failed to crawl Id: {id}! (Failed to Parse Json) Continue crawling in 1 minute...");
                lock (_lock)
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to crawl Id: {id}!");
                Console.WriteLine(ex);

                _fail_count++;

                lock (_lock)
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                return false;
            }

            return true;
        }
    }
}