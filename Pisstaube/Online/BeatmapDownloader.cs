using System.IO;
using osu.Framework.Extensions;
using osu.Framework.IO.Network;
using osu.Framework.Platform;
using osu.Game.IO;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Database.Models;
using FileInfo = osu.Game.IO.FileInfo;

namespace Pisstaube.Online
{
    public class BeatmapDownloader
    {
        private readonly FileStore _store;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly Storage _tmpStorage;

        public BeatmapDownloader(FileStore store, Storage dataStorage, PisstaubeCacheDbContextFactory cache)
        {
            _store = store;
            _cache = cache;
            _tmpStorage = dataStorage.GetStorageForDirectory("tmp");
        }
        
        public FileInfo Download(ChildrenBeatmap beatmap)
        {
            var osuFileString = $"{beatmap.Parent.Artist} - {beatmap.Parent.Title} ({beatmap.Parent.Creator}) [{beatmap.DiffName}].osu";
            var req = new FileWebRequest(
                _tmpStorage.GetFullPath(osuFileString.GetHashCode().ToString(), true),
                $"https://osu.ppy.sh/osu/{osuFileString}");
            req.Perform();

            FileInfo info;
            using (var f = _tmpStorage.GetStream(osuFileString.GetHashCode().ToString(), FileAccess.Read, FileMode.Open))
            using (var db = _cache.GetForWrite())
            {
                info = _store.Add(f);
                db.Context.CacheBeatmaps.Add(new Beatmap {BeatmapId = beatmap.BeatmapId, Hash = info.Hash, FileMd5 = f.ComputeMD5Hash()});
            }
            
            _tmpStorage.Delete(osuFileString);

            return info;
        }
    }
}