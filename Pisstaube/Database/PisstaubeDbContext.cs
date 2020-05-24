using Microsoft.EntityFrameworkCore;
using Pisstaube.Database.Models;

namespace Pisstaube.Database
{
    public class PisstaubeDbContext : DbContext
    {
        public DbSet<BeatmapSet> BeatmapSet { get; set; }
        public DbSet<ChildrenBeatmap> Beatmaps { get; set; }
        
        private static readonly bool[] Migrated = {false};
        
        public PisstaubeDbContext(DbContextOptions options) : base(options)
        {
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BeatmapSet>()
                .HasMany(j => j.ChildrenBeatmaps)
                .WithOne(j => j.Parent)
                .HasForeignKey(j => j.ParentSetId);
        }
    }
}