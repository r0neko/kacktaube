using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Pisstaube.CacheDb;

namespace Pisstaube.Utils
{
    public class SmartStorage
    {
        private readonly PisstaubeCacheDbContextFactory cache;
        private readonly ulong maxSize;
        private readonly Storage cacheStorage;

        public ulong MaxSize => maxSize;
        public long DataDirectorySize { get; private set; }

        public SmartStorage(Storage storage, PisstaubeCacheDbContextFactory cache)
        {
            this.cache = cache;
            var maximumSize = Environment.GetEnvironmentVariable("CLEANER_MAX_SIZE");
            Debug.Assert(maximumSize != null, nameof(maximumSize) + " != null");

            switch (maximumSize[^1])
            {
                case 'b':
                case 'B':
                    ulong.TryParse(maximumSize.Remove(maximumSize.Length - 1), out maxSize);
                    if (maxSize == 0)
                        maxSize = 536870912000; // 500 gb
                    break;

                case 'k':
                case 'K':
                    ulong.TryParse(maximumSize.Remove(maximumSize.Length - 1), out maxSize);
                    if (maxSize == 0)
                        maxSize = 536870912000; // 500 gb
                    else
                        maxSize *= 1024;
                    break;

                case 'm':
                case 'M':
                    ulong.TryParse(maximumSize.Remove(maximumSize.Length - 1), out maxSize);
                    if (maxSize == 0)
                        maxSize = 536870912000; // 500 gb
                    else
                        maxSize *= 1048576;
                    break;

                case 'g':
                case 'G':
                    ulong.TryParse(maximumSize.Remove(maximumSize.Length - 1), out maxSize);
                    if (maxSize == 0)
                        maxSize = 536870912000; // 500 gb
                    else
                        maxSize *= 1073741824;
                    break;

                case 't':
                case 'T':
                    ulong.TryParse(maximumSize.Remove(maximumSize.Length - 1), out maxSize);
                    if (maxSize == 0)
                        maxSize = 536870912000; // 500 gb
                    else
                        maxSize *= 1099511627776;
                    break;

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    long.TryParse(maximumSize, out var x);
                    if (x == 0)
                        maxSize = 536870912000; // 500 gb
                    break;

                default:
                    ulong.TryParse(maximumSize.Remove(maximumSize.Length - 1), out this.maxSize);
                    if (maxSize == 0)
                        maxSize = 536870912000; // 500 gb
                    break;
            }

            cacheStorage = storage.GetStorageForDirectory("cache");

            var info = new DirectoryInfo(cacheStorage.GetFullPath("./"));
            DataDirectorySize = info.EnumerateFiles().Sum(file => file.Length);
        }

        private bool IsFitting(long size) => (ulong) (size + DataDirectorySize) <= maxSize;

        public void IncreaseSize(long size) => DataDirectorySize += size;

        public bool FreeStorage()
        {
            for (var i = 0; i < 1000; i++)
            {
                Logger.LogPrint($"FreeStorage (DirectorySize: {DataDirectorySize} MaxSize: {maxSize})");
                if (IsFitting(0)) return true;

                Logger.LogPrint("Freeing Storage");

                using (var db = cache.GetForWrite())
                {
                    var map = db.Context.CacheBeatmapSet.FirstOrDefault(cbs =>
                        (cbs.LastDownload - DateTime.Now).TotalDays < 7);
                    if (map != null)
                    {
                        db.Context.CacheBeatmapSet.Remove(map);
                        db.Context.SaveChanges();
                        if (!cacheStorage.Exists(map.SetId.ToString("x8")))
                            continue;

                        DataDirectorySize -= new FileInfo(cacheStorage.GetFullPath(map.SetId.ToString("x8"))).Length;
                        if (DataDirectorySize < 0)
                            DataDirectorySize = 0;

                        cacheStorage.Delete(map.SetId.ToString("x8"));
                        db.Context.SaveChanges();
                    }
                    else
                    {
                        map = db.Context.CacheBeatmapSet.OrderByDescending(cbs => cbs.LastDownload)
                            .ThenByDescending(cbs => cbs.DownloadCount).FirstOrDefault();

                        if (map == null) continue;
                        db.Context.CacheBeatmapSet.Remove(map);
                        db.Context.SaveChanges();
                        if (!cacheStorage.Exists(map.SetId.ToString("x8")))
                            continue;

                        DataDirectorySize -= new FileInfo(cacheStorage.GetFullPath(map.SetId.ToString("x8"))).Length;
                        if (DataDirectorySize < 0)
                            DataDirectorySize = 0;

                        cacheStorage.Delete(map.SetId.ToString("x8"));
                        db.Context.SaveChanges();
                    }
                }
            }

            return false; // Failed to FreeStorage!
        }
    }
}