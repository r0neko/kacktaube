using System;
using System.Linq;
using System.Threading;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using Pisstaube.Database;
using Pisstaube.Engine;
using Pisstaube.Online.Crawler;
using Pisstaube.Utils;
using Sentry;

namespace Pisstaube.Online
{
    public class DatabaseHouseKeeper : OsuCrawler
    {
        private readonly RequestLimiter _requestLimiter;
        private readonly IAPIProvider _apiProvider;
        private readonly PisstaubeDbContext _dbContext;
        private readonly IBeatmapSearchEngineProvider _searchEngine;
        
        public int ToUpdate { get; private set; }
        public int Remaining { get; private set; }
        
        protected override void ThreadWorker()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {
                    var beatmaps = _dbContext.BeatmapSet
                        .Where(x => !x.Disabled)
                        .AsEnumerable()
                        .Where(
                            x => x.LastChecked != null && ((
                                                               x.RankedStatus == BeatmapSetOnlineStatus.None ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Graveyard ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Pending ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Ranked ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Loved
                                                           ) &&
                                                           (x.LastChecked.Value + TimeSpan.FromDays(30))
                                                           .Subtract(DateTime.Now).TotalMilliseconds < 0 ||
                                                           (
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Qualified ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.WIP
                                                           ) &&
                                                           (x.LastChecked.Value + TimeSpan.FromDays(1))
                                                           .Subtract(DateTime.Now).TotalMilliseconds < 0 ||
                                                           x.RankedStatus == BeatmapSetOnlineStatus.Approved &&
                                                           (x.LastChecked.Value + TimeSpan.FromDays(90))
                                                           .Subtract(DateTime.Now).TotalMilliseconds < 0)
                        );
                    
                    foreach (var beatmap in beatmaps)
                    {
                        if (Tasks.Count > 128) {
                            foreach (var task in Tasks) // wait for all tasks
                            {
                                task.Wait(CancellationToken);
                            }
                        
                            Tasks.Clear(); // Remove all previous tasks.
                        }
                        
                        _requestLimiter.Limit();
                        
                        Tasks.Add(Crawl(beatmap.SetId));
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "an Unknown error occured during HouseKeeping", LoggingTarget.Database);
                    SentrySdk.CaptureException(e);
                }
                
                Thread.Sleep(TimeSpan.FromHours(8)); // Update every 8 hours...
            }
        }

        public DatabaseHouseKeeper(Storage storage, RequestLimiter requestLimiter, IAPIProvider apiProvider, IBeatmapSearchEngineProvider searchEngine, BeatmapDownloader beatmapDownloader) : base(storage, requestLimiter, apiProvider, searchEngine, beatmapDownloader)
        {
            _requestLimiter = requestLimiter;
            _apiProvider = apiProvider;
            _dbContext = new PisstaubeDbContext();
            _searchEngine = searchEngine;
        }
    }
}