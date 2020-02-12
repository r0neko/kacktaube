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
            public string IpfsHash;
        }

        private readonly Storage _storage;
        private readonly IAPIProvider _apiProvider;
        private readonly PisstaubeDbContext _dbContext;
        private readonly object _dbContextMutes = new object();
        private readonly PisstaubeCacheDbContextFactory _cacheFactory;
        private readonly SmartStorage _smartStorage;
        private readonly RequestLimiter _limiter;
        private readonly IBeatmapSearchEngineProvider _search;
        private readonly IpfsCache _ipfsCache;

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
            this._storage = storage;
            this._apiProvider = apiProvider;
            this._dbContext = dbContext;
            this._cacheFactory = cacheFactory;
            this._smartStorage = smartStorage;
            this._limiter = limiter;
            this._search = search;
            this._ipfsCache = ipfsCache;
        }

        public DownloadMapResponse DownloadMap(int beatmapSetId, bool dlVideo = false, bool ipfs = false)
        {
            if (_apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                throw new UnauthorizedAccessException("API Is not Authorized!");
            }

            if (!_storage.ExistsDirectory("cache"))
                _storage.GetFullPath("cache", true);

            BeatmapSet set;
            lock (_dbContextMutes)
            {
                if ((set = _dbContext.BeatmapSet
                        .FirstOrDefault(bmSet => bmSet.SetId == beatmapSetId && !bmSet.Disabled)) == null)
                    throw new LegacyScoreParser.BeatmapNotFoundException();
            }
                
            var cacheStorage = _storage.GetStorageForDirectory("cache");
            var bmFileId = beatmapSetId.ToString("x8") + (dlVideo ? "" : "_novid");

            CacheBeatmapSet cachedMap;
            if (!cacheStorage.Exists(bmFileId))
            {
                if (!_smartStorage.FreeStorage())
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
                _limiter.Limit();
                req.Perform(_apiProvider);

                using (var f = cacheStorage.GetStream(bmFileId, FileAccess.Write))
                {
                    using var readStream = File.OpenRead(tmpFile);
                    readStream.CopyTo(f);
                }

                _smartStorage.IncreaseSize(new FileInfo(tmpFile).Length);
                File.Delete(tmpFile);

                using var db = _cacheFactory.GetForWrite();
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

                //DogStatsd.Increment("beatmap.downloads");

                var cac = _ipfsCache.CacheFile("cache/" + bmFileId);
                
                return new DownloadMapResponse {
                    File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                    FileStream = !ipfs || cac.Result == "" ? cacheStorage.GetStream (bmFileId, FileAccess.Read, FileMode.Open) : null, // Don't even bother opening a stream.
                    IpfsHash = cac.Result,
                };
            }

            using (var db = _cacheFactory.GetForWrite())
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

            var cache = _ipfsCache.CacheFile("cache/" + bmFileId);
            
            return new DownloadMapResponse {
                File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                FileStream = !ipfs || cache.Result == "" ? cacheStorage.GetStream (bmFileId, FileAccess.Read, FileMode.Open) : null, // Don't even bother opening a stream.
                IpfsHash = cache.Result,
            };
        }
    }
}