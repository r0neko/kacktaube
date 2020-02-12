using System.IO;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.IO.Network;
using osu.Framework.Platform;
using osu.Game.IO;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Database.Models;
using Pisstaube.Utils;
using FileInfo = osu.Game.IO.FileInfo;

namespace Pisstaube.Online
{
    public class BeatmapDownloader
    {
        private readonly FileStore _store;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly RequestLimiter _limiter;
        private readonly Storage _tmpStorage;

        public BeatmapDownloader(FileStore store, Storage dataStorage, PisstaubeCacheDbContextFactory cache,
            RequestLimiter limiter)
        {
            _store = store;
            _cache = cache;
            _limiter = limiter;
            _tmpStorage = dataStorage.GetStorageForDirectory("tmp");
        }

        public FileInfo Download(ChildrenBeatmap beatmap)
        {
            var req = new FileWebRequest(
                _tmpStorage.GetFullPath(beatmap.BeatmapId.ToString(), true),
                $"https://osu.ppy.sh/osu/{beatmap.BeatmapId}");

            _limiter.Limit();

            req.Perform();

            FileInfo info;
            using (var f = _tmpStorage.GetStream(beatmap.BeatmapId.ToString(), FileAccess.Read, FileMode.Open))
            {
                using var db = _cache.GetForWrite();
                info = _store.Add(f);

                if (db.Context.CacheBeatmaps.Any(bm => bm.BeatmapId == beatmap.BeatmapId))
                    db.Context.CacheBeatmaps.Update(new Beatmap
                        {BeatmapId = beatmap.BeatmapId, Hash = info.Hash, FileMd5 = f.ComputeMD5Hash()});
                else
                    db.Context.CacheBeatmaps.Add(new Beatmap
                        {BeatmapId = beatmap.BeatmapId, Hash = info.Hash, FileMd5 = f.ComputeMD5Hash()});
            }

            _tmpStorage.Delete(beatmap.BeatmapId.ToString());

            return info;
        }
    }
}