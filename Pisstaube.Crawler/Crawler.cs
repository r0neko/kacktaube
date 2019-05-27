using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using opi.v1;
using osu.Framework.Logging;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Crawler
{
    public class Crawler
    {
        private bool _should_stop;
        private int _latest_Id;
        private int _fail_count;
        
        private object _lock = new object();
        private List<Thread> _pool;
        
        private readonly Api _api;
        private readonly BeatmapSearchEngine _search;
        private readonly int _workerThreads;
        private Thread _thread_restarter;

        public Crawler(BeatmapSearchEngine search)
        {
            _pool = new List<Thread>();
            _search = search;
            _workerThreads = int.Parse(Environment.GetEnvironmentVariable("CRAWLER_THREADS"));
            _api = new Api(Environment.GetEnvironmentVariable("OSU_API_KEY"));
        }

        public void BeginCrawling()
        {
            while (true)
            {
                if (_thread_restarter == null)
                    _thread_restarter = new Thread(() =>
                    {
                        _should_stop = false;

                        while (true)
                        {
                            for (var i = 0; i < _workerThreads; i++)
                            {
                                _pool.Add(new Thread(_crawl));

                                _pool.Last().Start();
                            }

                            while (!_should_stop) Thread.Sleep(50);

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

                _thread_restarter.Start();
                break;
            }
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
                var id = 0;
                try
                {
                    lock (_lock)
                        id = _latest_Id++;

                    Logger.LogPrint($"Crawling Id: {_latest_Id}");
                    
                    var maps = _api.GetBeatmapSet(id);
                    if (maps == null || maps.Length == 0)
                    {
                        _fail_count++;
                        continue;
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
                        pmap.MaxCombo = 0;
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
                catch (DbUpdateException)
                {
                    _context = new PisstaubeDbContext();
                    Console.WriteLine($"Failed to crawl Id: {_latest_Id}! (DbUpdateException)");
                }
                catch (JsonException)
                {
                    Console.WriteLine($"Failed to crawl Id: {id}! (Failed to Parse Json) Continue crawling in 1 minute...");
                    lock (_lock)
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to crawl Id: {id}!");
                    Console.WriteLine(ex);

                    _fail_count++;

                    lock (_lock)
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                }
                
                if (_fail_count > 50) // We failed 50 times, lets try tomorrow again! maybe there are new exciting beatmaps!
                    _should_stop = true;
            }
            
            _context.Dispose();
        }
    }
}