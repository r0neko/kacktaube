using System;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Scoring.Legacy;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Engine;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Online
{
    public class SetDownloader
    {
        private readonly Storage _storage;
        private readonly APIAccess _apiAccess;
        private readonly PisstaubeDbContextFactory _factory;
        private readonly PisstaubeCacheDbContextFactory _cfactory;
        private readonly Cleaner _cleaner;
        private readonly RequestLimiter _limiter;
        private readonly BeatmapSearchEngine _search;

        public SetDownloader(Storage storage,
            APIAccess apiAccess,
            PisstaubeDbContextFactory factory,
            PisstaubeCacheDbContextFactory cfactory,
            Cleaner cleaner,
            RequestLimiter limiter,
            BeatmapSearchEngine search
            )
        {
            _storage = storage;
            _apiAccess = apiAccess;
            _factory = factory;
            _cfactory = cfactory;
            _cleaner = cleaner;
            _limiter = limiter;
            _search = search;
        }

        public Tuple<string, Stream> DownloadMap(int beatmapSetId, bool dlVideo = false)
        {
            if (_apiAccess.State == APIState.Offline) {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                throw new UnauthorizedAccessException("API Is not Authorized!");
            }

            if (!_storage.ExistsDirectory("cache"))
                _storage.GetFullPath("cache", true);
            
            BeatmapSet set;
            if ((set = _factory.Get().BeatmapSet
                               .FirstOrDefault(bmSet => bmSet.SetId == beatmapSetId && !bmSet.Disabled)) == null)
                throw new LegacyScoreParser.BeatmapNotFoundException();

            var cacheStorage = _storage.GetStorageForDirectory("cache");
            var bmFileId = beatmapSetId.ToString("x8") + (dlVideo ? "" : "_novid");

            CacheBeatmapSet cachedMap;
            if (!cacheStorage.Exists(bmFileId)) {
                if (!_cleaner.FreeStorage())
                {
                    Logger.Error(new Exception("Cache Storage is full!"),
                        "Please change the Cleaner Settings!",
                        LoggingTarget.Database);

                    throw new IOException("Storage Full!");
                }

                try
                {
                    var req = new DownloadBeatmapSetRequest(new BeatmapSetInfo {OnlineBeatmapSetID = beatmapSetId}, !dlVideo);
                    
                    // Video download is not supported, to much traffic. almost no one download videos anyways!
                    
                    var tmpFile = string.Empty;
                    req.Success += c => tmpFile = c;
                    _limiter.Limit();
                    req.Perform(_apiAccess);
                
                    using (var f = cacheStorage.GetStream(bmFileId, FileAccess.Write)) 
                    using (var readStream = File.OpenRead(tmpFile))
                        readStream.CopyTo(f);
                
                    _cleaner.IncreaseSize(new FileInfo(tmpFile).Length);
                    File.Delete(tmpFile);

                    using (var db = _cfactory.GetForWrite())
                    {
                        if ((cachedMap = db.Context.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) == null)
                        {
                            db.Context.CacheBeatmapSet.Add(new CacheBeatmapSet
                            {
                                SetId = set.SetId,
                                DownloadCount = 1,
                                LastDownload = DateTime.Now
                            });
                        }
                        else
                        {
                            cachedMap.DownloadCount++;
                            cachedMap.LastDownload = DateTime.Now;
                            db.Context.CacheBeatmapSet.Update(cachedMap);
                        }
                    }
                    
                } catch (ObjectDisposedException)
                {
                    // Cannot access a closed file.
                    // Beatmap got DMCA'd
                    
                    _search.DeleteBeatmap(beatmapSetId);

                    using (var db = _factory.GetForWrite())
                    {
                        set.Disabled = true;
                        db.Context.BeatmapSet.Update(set);
                    }

                    throw new NotSupportedException("Beatmap got DMCA'd");
                }
            
                DogStatsd.Increment("beatmap.downloads");
                
                return Tuple.Create(
                    $"{set.SetId} {set.Artist} - {set.Title}.osz",
                    cacheStorage.GetStream(bmFileId));
            }

            using (var db = _cfactory.GetForWrite())
            {
                if ((cachedMap = db.Context.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) == null)
                {
                    db.Context.CacheBeatmapSet.Add(new CacheBeatmapSet
                    {
                        SetId = set.SetId,
                        DownloadCount = 1,
                        LastDownload = DateTime.Now
                    });
                }
                else
                {
                    cachedMap.DownloadCount++;
                    cachedMap.LastDownload = DateTime.Now;
                    db.Context.CacheBeatmapSet.Update(cachedMap);
                }
            }

            DogStatsd.Increment("beatmap.downloads");
            
            return Tuple.Create(
                $"{set.SetId} {set.Artist} - {set.Title}.osz",
                cacheStorage.GetStream(bmFileId)
                );
        }
    }
}
