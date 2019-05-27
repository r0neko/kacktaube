using Microsoft.EntityFrameworkCore;
using osu.Framework.Platform;
using Pisstaube.CacheDb.Models;

namespace Pisstaube.CacheDb
{
    public class PisstaubeCacheDbContext : DbContext
    {
        private readonly Storage _storage;
        public DbSet<CacheBeatmapSet> CacheBeatmapSet { get; set; }
        
        private static readonly bool[] Migrated = {false};
        public PisstaubeCacheDbContext(Storage storage = null)
        {
            _storage = storage ?? new NativeStorage("data");
            
            if (Migrated[0]) return;
            lock (Migrated)
            {
                if (Migrated[0]) return;
                Database.Migrate();
                Migrated[0] = true;
            }
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            optionsBuilder.UseSqlite(_storage.GetDatabaseConnectionString("cache"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}