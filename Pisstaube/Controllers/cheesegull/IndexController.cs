using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Scoring.Legacy;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Online;
using Pisstaube.Utils;

namespace Pisstaube.Controllers
{
    [Route("")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        private readonly IAPIProvider apiProvider;
        private readonly Storage storage;
        private readonly Storage fileStorage;
        private readonly PisstaubeDbContext dbContext;
        private readonly object dbContextLock = new object();
        private readonly PisstaubeCacheDbContextFactory cache;
        private readonly BeatmapDownloader downloader;
        private readonly SetDownloader setDownloader;

        public IndexController(IAPIProvider apiProvider,
            Storage storage,
            PisstaubeCacheDbContextFactory cache,
            BeatmapDownloader downloader,
            SetDownloader setDownloader,
            PisstaubeDbContext dbContext)
        {
            this.apiProvider = apiProvider;
            this.storage = storage;
            this.cache = cache;
            this.downloader = downloader;
            this.setDownloader = setDownloader;
            this.dbContext = dbContext;
            fileStorage = storage.GetStorageForDirectory("files");
        }
        
        // GET /osu/:beatmapId
        [HttpGet("osu/{beatmapId:int}")]
        public ActionResult GetBeatmap(int beatmapId)
        {
            var hash = cache.Get().CacheBeatmaps.Where(bm => bm.BeatmapId == beatmapId).Select(bm => bm.Hash)
                .FirstOrDefault();

            if (hash == null)
                lock (dbContextLock)
                {
                    foreach (var map in dbContext.Beatmaps.Where(bm => bm.BeatmapId == beatmapId))
                    {
                        var fileInfo = downloader.Download(map);

                        map.FileMd5 = cache.Get()
                            .CacheBeatmaps
                            .Where(cmap => cmap.Hash == fileInfo.Hash)
                            .Select(cmap => cmap.FileMd5)
                            .FirstOrDefault();
                    }
                }


            var info = new osu.Game.IO.FileInfo {Hash = hash};

            return File(fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }

        // GET /osu/:fileMd5
        [HttpGet("osu/{fileMd5}")]
        public ActionResult GetBeatmap(string fileMd5)
        {
            var hash = cache.Get().CacheBeatmaps.Where(bm => bm.FileMd5 == fileMd5).Select(bm => bm.Hash)
                .FirstOrDefault();

            if (hash == null)
                lock (dbContextLock)
                {
                    foreach (var map in dbContext.Beatmaps.Where(bm => bm.FileMd5 == fileMd5))
                    {
                        var fileInfo = downloader.Download(map);

                        map.FileMd5 = cache.Get()
                            .CacheBeatmaps
                            .Where(cMap => cMap.Hash == fileInfo.Hash)
                            .Select(cMap => cMap.FileMd5)
                            .FirstOrDefault();

                        hash = fileInfo.Hash;
                    }
                }


            var info = new osu.Game.IO.FileInfo {Hash = hash};

            return File(fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }

        // GET /d/:SetId
        [HttpGet("d/{beatmapSetId:int}")]
        public ActionResult GetSet(int beatmapSetId, bool ipfs = false)
        {
            if (apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                return StatusCode(503, "Osu! API is not available.");
            }

            SetDownloader.DownloadMapResponse r;
            try
            {
                r = setDownloader.DownloadMap(beatmapSetId, !Request.Query.ContainsKey("novideo"), ipfs);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(503, "Osu! API is not available.");
            }
            catch (LegacyScoreParser.BeatmapNotFoundException)
            {
                return StatusCode(404, "Beatmap not Found!");
            }
            catch (IOException)
            {
                return StatusCode(500, "Storage Full!");
            }
            catch (NotSupportedException)
            {
                return StatusCode(404, "Beatmap got DMCA'd!");
            }
            
            if (ipfs && r.IPFSHash != "")
                return Ok(JsonUtil.Serialize(new
                {
                    Name = r.File,
                    Hash = r.IPFSHash
                }));
            
            if (r.FileStream != null)
                return File (r.FileStream,
                    "application/octet-stream",
                    r.File);

            return Ok("Failed to open stream!");
        }
        
        /*
         * People started to Reverse proxy /d/* to this Server, so we should take advantage of that and give the osu! Client a NoVideo option
         * WITHOUT ?novideo as the osu!client handles downloads like /d/{id}{novid ? n : ""}?us=.....&ha=.....
         */
        // GET /d/:SetId
        [HttpGet("d/{beatmapSetId:int}n")]
        public ActionResult GetSetNoVid(int beatmapSetId, bool ipfs = false)
        {
            if (apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                return StatusCode(503, "Osu! API is not available.");
            }

            SetDownloader.DownloadMapResponse r;
            try
            {
                r = setDownloader.DownloadMap(beatmapSetId, false, ipfs);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(503, "Osu! API is not available.");
            }
            catch (LegacyScoreParser.BeatmapNotFoundException)
            {
                return StatusCode(404, "Beatmap not Found!");
            }
            catch (IOException)
            {
                return StatusCode(500, "Storage Full!");
            }
            catch (NotSupportedException)
            {
                return StatusCode(404, "Beatmap got DMCA'd!");
            }
            
            if (ipfs && r.IPFSHash != "")
                return Ok(JsonUtil.Serialize(new
                {
                    Name = r.File,
                    Hash = r.IPFSHash
                }));
            
            if (r.FileStream != null)
                return File (r.FileStream,
                    "application/octet-stream",
                    r.File);

            return Ok("Failed to open stream!");
        }
    }
}