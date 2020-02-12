// <auto-generated />

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pisstaube.CacheDb;

namespace Pisstaube.CacheDb.Migrations
{
    [DbContext(typeof(PisstaubeCacheDbContext))]
    internal partial class PisstaubeCacheDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.4-servicing-10062");

            modelBuilder.Entity("olPisstaube.CacheDb.Models.Beatmap", b =>
            {
                b.Property<int>("BeatmapId")
                    .ValueGeneratedOnAdd();

                b.Property<string>("FileMd5");

                b.Property<string>("Hash");

                b.HasKey("BeatmapId");

                b.ToTable("CacheBeatmaps");
            });

            modelBuilder.Entity("olPisstaube.CacheDb.Models.CacheBeatmapSet", b =>
            {
                b.Property<int>("SetId")
                    .ValueGeneratedOnAdd();

                b.Property<long>("DownloadCount");

                b.Property<string>("Hash");

                b.Property<DateTime>("LastDownload");

                b.HasKey("SetId");

                b.ToTable("CacheBeatmapSet");
            });
#pragma warning restore 612, 618
        }
    }
}