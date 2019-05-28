using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Controllers
{
    [Route("d")] 
    [ApiController]
    public class BeatmapDownloadController : ControllerBase
    {
        private readonly APIAccess _apiv2;
        private readonly Cleaner _cleaner;
        private readonly Storage _storage;
        private readonly PisstaubeDbContext _context;
        private readonly PisstaubeCacheDbContext _cacheContext;

        public BeatmapDownloadController(APIAccess apiv2,
            Cleaner cleaner,
            Storage storage,
            PisstaubeDbContext context,
            PisstaubeCacheDbContext cacheContext)
        {
            _apiv2 = apiv2;
            _cleaner = cleaner;
            _storage = storage;
            _context = context;
            _cacheContext = cacheContext;
        }
        
        // GET /d/:SetId
        [HttpGet("{beatmapSetId:int}")]
        public ActionResult GetSet(int beatmapSetId)
        {
            if (_apiv2.State == APIState.Offline) {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);
                
                return StatusCode(503, "Osu! API is not available.");
            }

            if (!_storage.ExistsDirectory("cache"))
                _storage.GetFullPath("cache", true);

            BeatmapSet set;
            if ((set = _context.BeatmapSet.FirstOrDefault(bmset => bmset.SetId == beatmapSetId)) == null)
                return NotFound("Set not found");
            
            var cacheStorage = _storage.GetStorageForDirectory("cache");
            var bmFileId = beatmapSetId.ToString("x8");
            CacheBeatmapSet cachedMap;
            
            if (!cacheStorage.Exists(bmFileId)) {
                if (!_cleaner.FreeStorage())
                {
                    Logger.Error(new Exception("Cache Storage is full!"),
                        "Please change the Cleaner Settings!",
                        LoggingTarget.Database);
                
                    return StatusCode(500, "Storage full");
                }
                
                var req = new DownloadBeatmapSetRequest(new BeatmapSetInfo {OnlineBeatmapSetID = beatmapSetId}, true);
                // Video download is not supported, to much traffic. almost no one download videos anyways!
                
                var tmpFile = string.Empty;
                req.Success += c => tmpFile = c;
                req.Perform(_apiv2);
                
                using (var f = cacheStorage.GetStream(bmFileId, FileAccess.Write)) 
                using (var readStream = System.IO.File.OpenRead(tmpFile))
                    readStream.CopyTo(f);
                
                _cleaner.IncreaseSize(new FileInfo(tmpFile).Length);
                System.IO.File.Delete(tmpFile);
                
                if ((cachedMap = _cacheContext.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) == null)
                {
                    _cacheContext.CacheBeatmapSet.Add(new CacheBeatmapSet
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
                    _cacheContext.CacheBeatmapSet.Update(cachedMap);
                }

                _cacheContext.SaveChanges();

                DogStatsd.Increment("beatmap.downloads");
                
                return File(cacheStorage.GetStream(beatmapSetId.ToString("x8")),
                    "application/octet-stream",
                    $"{set.SetId} {set.Artist} - {set.Title}.osz");
            }
            
            if ((cachedMap = _cacheContext.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) == null)
            {
                _cacheContext.CacheBeatmapSet.Add(new CacheBeatmapSet
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
                _cacheContext.CacheBeatmapSet.Update(cachedMap);
            }
            _cacheContext.SaveChanges();
            
            DogStatsd.Increment("beatmap.downloads");

            return File(cacheStorage.GetStream(beatmapSetId.ToString("x8")),
                "application/octet-stream",
                $"{set.SetId} {set.Artist} - {set.Title}.osz");
        }
    }
}