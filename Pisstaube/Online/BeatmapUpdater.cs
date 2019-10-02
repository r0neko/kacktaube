using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.File;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Engine;
using Pisstaube.Utils;

namespace Pisstaube.Online
{
    public class BeatmapUpdater
    {
        private readonly PisstaubeDbContextFactory _factory;
        private readonly APIAccess _apiAccess;
        private readonly BeatmapDownloader _bmDl;
        private readonly PisstaubeCacheDbContextFactory _cFactory;
        private readonly RulesetStore _store;
        private readonly Storage _storage;
        private readonly BeatmapSearchEngine _search;
        private readonly Crawler.Crawler crawler;
        private RequestLimiter _rl;
        
        public BeatmapUpdater(PisstaubeDbContextFactory factory,
            APIAccess apiAccess,
            BeatmapDownloader bmDl,
            PisstaubeCacheDbContextFactory cFactory,
            RulesetStore store,
            Storage storage,
            BeatmapSearchEngine search,
            Crawler.Crawler crawler,
            
            int limit = 100 /* 100 beatmaps can be updated at the same time per minute! */)
        {
            _factory = factory;
            _apiAccess = apiAccess;
            _bmDl = bmDl;
            _cFactory = cFactory;
            _store = store;
            _storage = storage;
            _search = search;
            this.crawler = crawler;
            _rl = new RequestLimiter(limit, TimeSpan.FromMinutes(1));
        }

        public async void BeginUpdaterAsync() => await Task.Run(BeginUpdaterSync);
        
        public void BeginUpdaterSync()
        {
            while (true)
            {
                if (crawler.IsCrawling) // Preventing a random crash while crawling!
                {
                    Thread.Sleep(1000);
                    continue;
                }

                var beatmapSets = _factory.Get().BeatmapSet.Where(x => !x.Disabled)
                    .Where(
                        x => x.LastChecked != null && ((
                                                           (
                                                               x.RankedStatus == BeatmapSetOnlineStatus.None ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Graveyard ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Pending ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Ranked ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Loved
                                                           ) &&
                                                           (x.LastChecked.Value + TimeSpan.FromDays(30))
                                                           .Subtract(DateTime.Now).TotalMilliseconds < 0
                                                       ) ||
                                                       (
                                                           (
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Qualified ||
                                                               x.RankedStatus == BeatmapSetOnlineStatus.WIP
                                                           ) &&
                                                           (x.LastChecked.Value + TimeSpan.FromDays(1))
                                                           .Subtract(DateTime.Now).TotalMilliseconds < 0
                                                       ) ||
                                                       (
                                                           (
                                                               x.RankedStatus == BeatmapSetOnlineStatus.Approved
                                                           ) &&
                                                           (x.LastChecked.Value + TimeSpan.FromDays(90))
                                                           .Subtract(DateTime.Now).TotalMilliseconds < 0
                                                       ))
                    );

                foreach (var bmSet in beatmapSets.ToList())
                {
                    try
                    {
                        bmSet.ChildrenBeatmaps =
                            _factory.Get().Beatmaps.Where(x => x.ParentSetId == bmSet.SetId).ToList();
                        var setRequest = new GetBeatmapSetRequest(bmSet.SetId);
                        _rl.Limit();
                        setRequest.Perform(_apiAccess);

                        Logger.LogPrint("Updating BeatmapSetId " + bmSet.SetId);

                        var setInfo = setRequest.Result.ToBeatmapSet(_store);
                        var newBm = BeatmapSet.FromBeatmapSetInfo(setInfo);

                        using (var db = _factory.GetForWrite())
                        {
                            var hasChanged = false;
                            foreach (var cb in newBm.ChildrenBeatmaps)
                            {
                                var fInfo = _bmDl.Download(cb);
                                var ha = _cFactory.Get().CacheBeatmaps.Where(b => b.Hash == fInfo.Hash)
                                    .Select(f => f.FileMd5).FirstOrDefault();
                                cb.FileMd5 = ha;

                                db.Context.Entry(cb).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                db.Context.Beatmaps.Update(cb);

                                if (bmSet.ChildrenBeatmaps.Any(x => x.FileMd5 == ha))
                                    continue;

                                hasChanged = true;
                            }

                            if (newBm.ChildrenBeatmaps.Count > bmSet.ChildrenBeatmaps.Count)
                                hasChanged = true;

                            var bmFileId = newBm.SetId.ToString("x8");
                            var bmFileIdNoVid = newBm.SetId.ToString("x8") + "_novid";

                            if (hasChanged)
                            {
                                _storage.GetStorageForDirectory("cache").Delete(bmFileId);
                                _storage.GetStorageForDirectory("cache").Delete(bmFileIdNoVid);

                                _search.DeleteBeatmap(newBm.SetId);
                                _search.IndexBeatmap(newBm);
                            }

                            db.Context.Entry(newBm).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            db.Context.BeatmapSet.Update(newBm);
                        }

                        FileSafety.DeleteCleanupDirectory();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Unknown Error while updating!");
                    }
                }
            }
        }
    }
}
