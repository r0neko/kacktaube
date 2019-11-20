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

namespace Pisstaube.Controllers
{
    [Route("")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        private readonly APIAccess _apiAccess;
        private readonly Storage _storage;
        private readonly Storage _fileStorage;
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly BeatmapDownloader _downloader;
        private readonly SetDownloader _setDownloader;

        public IndexController(APIAccess apiAccess,
            Storage storage,
            PisstaubeCacheDbContextFactory cache,
            BeatmapDownloader downloader,
            SetDownloader setDownloader,
            PisstaubeDbContextFactory contextFactory)
        {
            _apiAccess = apiAccess;
            _storage = storage;
            _cache = cache;
            _downloader = downloader;
            _setDownloader = setDownloader;
            _contextFactory = contextFactory;
            _fileStorage = storage.GetStorageForDirectory("files");
        }

        // GET /
        [HttpGet]
        public ActionResult Get()
        {
            var f = _storage.GetStream("pisse.html", FileAccess.ReadWrite);

            if (f.Length != 0)
                return Ok(f);

            f.Write(Encoding.UTF8.GetBytes(
                "Running Pisstaube, a fuck off of cheesegull Written by Mempler available on Github under MIT License!"));
            f.Flush();
            f.Position = 0;

            return Ok(f);
        }

        // GET /osu/:beatmapId
        [HttpGet("osu/{beatmapId:int}")]
        public ActionResult GetBeatmap(int beatmapId)
        {
            var hash = _cache.Get().CacheBeatmaps.Where(bm => bm.BeatmapId == beatmapId).Select(bm => bm.Hash)
                .FirstOrDefault();

            if (hash == null)
                foreach (var map in _contextFactory.Get().Beatmaps.Where(bm => bm.BeatmapId == beatmapId))
                {
                    var fileInfo = _downloader.Download(map);

                    map.FileMd5 = _cache.Get()
                        .CacheBeatmaps
                        .Where(cmap => cmap.Hash == fileInfo.Hash)
                        .Select(cmap => cmap.FileMd5)
                        .FirstOrDefault();
                }

            var info = new osu.Game.IO.FileInfo {Hash = hash};

            return File(_fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }

        // GET /osu/:fileMd5
        [HttpGet("osu/{fileMd5}")]
        public ActionResult GetBeatmap(string fileMd5)
        {
            var hash = _cache.Get().CacheBeatmaps.Where(bm => bm.FileMd5 == fileMd5).Select(bm => bm.Hash)
                .FirstOrDefault();

            if (hash == null)
                foreach (var map in _contextFactory.Get().Beatmaps.Where(bm => bm.FileMd5 == fileMd5))
                {
                    var fileInfo = _downloader.Download(map);

                    map.FileMd5 = _cache.Get()
                        .CacheBeatmaps
                        .Where(cMap => cMap.Hash == fileInfo.Hash)
                        .Select(cMap => cMap.FileMd5)
                        .FirstOrDefault();

                    hash = fileInfo.Hash;
                }

            var info = new osu.Game.IO.FileInfo {Hash = hash};

            return File(_fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }

        // GET /d/:SetId
        [HttpGet("d/{beatmapSetId:int}")]
        public ActionResult GetSet(int beatmapSetId)
        {
            if (_apiAccess.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                return StatusCode(503, "Osu! API is not available.");
            }

            Tuple<string, Stream> r;
            try
            {
                r = _setDownloader.DownloadMap(beatmapSetId, !Request.Query.ContainsKey("novideo"));
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

            return File(r.Item2,
                "application/octet-stream",
                r.Item1);
        }
    }
}