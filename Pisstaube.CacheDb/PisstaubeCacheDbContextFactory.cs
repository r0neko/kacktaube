using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using osu.Framework.Platform;

namespace Pisstaube.CacheDb
{
    public class PisstaubeCacheDbContextFactory : IDesignTimeDbContextFactory<PisstaubeCacheDbContext>
    {
        private readonly Storage _storage;

        public PisstaubeCacheDbContextFactory()
        {
            _storage = new NativeStorage("data");
        }

        public PisstaubeCacheDbContext CreateDbContext(string[] args)
        {
            return new PisstaubeCacheDbContext(_storage);
        }
    }
}