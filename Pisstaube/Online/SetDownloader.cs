using System;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Scoring.Legacy;
using Pisstaube.Allocation;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Engine;
using Pisstaube.Utils;

namespace Pisstaube.Online
{
    public class SetDownloader
    {
        public class DownloadMapResponse
        {
            public string File;
            public Stream FileStream;
            public string IPFSHash;
        }

        private readonly Storage storage;
        private readonly IAPIProvider apiProvider;
        private readonly PisstaubeDbContext dbContext;
        private readonly object dbContextMutes = new object();
        private readonly PisstaubeCacheDbContextFactory cacheFactory;
        private readonly SmartStorage smartStorage;
        private readonly RequestLimiter limiter;
        private readonly IBeatmapSearchEngineProvider search;
        private readonly IpfsCache ipfsCache;

        public SetDownloader(Storage storage,
            IAPIProvider apiProvider,
            PisstaubeDbContext dbContext,
            PisstaubeCacheDbContextFactory cacheFactory,
            SmartStorage smartStorage,
            RequestLimiter limiter,
            IBeatmapSearchEngineProvider search,
            IpfsCache ipfsCache
        )
        {
            this.storage = storage;
            this.apiProvider = apiProvider;
            this.dbContext = dbContext;
            this.cacheFactory = cacheFactory;
            this.smartStorage = smartStorage;
            this.limiter = limiter;
            this.search = search;
            this.ipfsCache = ipfsCache;
        }

        public DownloadMapResponse DownloadMap(int beatmapSetId, bool dlVideo = false, bool ipfs = false)
        {
            if (apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                throw new UnauthorizedAccessException("API Is not Authorized!");
            }

            if (!storage.ExistsDirectory("cache"))
                storage.GetFullPath("cache", true);

            BeatmapSet set;
            lock (dbContextMutes)
            {
                if ((set = dbContext.BeatmapSet
                        .FirstOrDefault(bmSet => bmSet.SetId == beatmapSetId && !bmSet.Disabled)) == null)
                    throw new LegacyScoreParser.BeatmapNotFoundException();
            }
                
            var cacheStorage = storage.GetStorageForDirectory("cache");
            var bmFileId = beatmapSetId.ToString("x8") + (dlVideo ? "" : "_novid");

            CacheBeatmapSet cachedMap;
            if (!cacheStorage.Exists(bmFileId))
            {
                if (!smartStorage.FreeStorage())
                {
                    Logger.Error(new Exception("Cache Storage is full!"),
                        "Please change the Cleaner Settings!",
                        LoggingTarget.Database);

                    throw new IOException("Storage Full!");
                }

                var req = new DownloadBeatmapSetRequest(new BeatmapSetInfo {OnlineBeatmapSetID = beatmapSetId},
                    !dlVideo);

                // Video download is not supported, to much traffic. almost no one download videos anyways!

                var tmpFile = string.Empty;
                req.Success += c => tmpFile = c;
                limiter.Limit();
                req.Perform(apiProvider);

                using (var f = cacheStorage.GetStream(bmFileId, FileAccess.Write))
                {
                    using var readStream = File.OpenRead(tmpFile);
                    readStream.CopyTo(f);
                }

                smartStorage.IncreaseSize(new FileInfo(tmpFile).Length);
                File.Delete(tmpFile);

                using var db = cacheFactory.GetForWrite();
                if ((cachedMap = db.Context.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) ==
                    null)
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

                /*
                catch (ObjectDisposedException)
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
                */

                //DogStatsd.Increment("beatmap.downloads");

                var cac = ipfsCache.CacheFile("cache/" + bmFileId);
                
                return new DownloadMapResponse {
                    File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                    FileStream = !ipfs && cac.Result == "" ? cacheStorage.GetStream (bmFileId) : null, // Don't even bother opening a stream.
                    IPFSHash = cac.Result,
                };
            }

            using (var db = cacheFactory.GetForWrite())
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

            //DogStatsd.Increment("beatmap.downloads");

            var cache = ipfsCache.CacheFile("cache/" + bmFileId);
            
            return new DownloadMapResponse {
                File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                FileStream = !ipfs && cache.Result == "" ? cacheStorage.GetStream (bmFileId) : null, // Don't even bother opening a stream.
                IPFSHash = cache.Result,
            };
        }
    }
}