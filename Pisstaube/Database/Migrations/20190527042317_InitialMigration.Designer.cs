// <auto-generated />
using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Pisstaube.Database;

namespace Pisstaube.Database.Migrations
{
    [DbContext (typeof (PisstaubeDbContext))]
    [Migration ("20190527042317_InitialMigration")]
    partial class InitialMigration
    {
        protected override void BuildTargetModel (ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation ("ProductVersion", "2.2.4-servicing-10062")
                .HasAnnotation ("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity ("Pisstaube.Database.Models.BeatmapSet", b =>
            {
                b.Property<int> ("SetId")
                    .ValueGeneratedOnAdd ( );

                b.Property<DateTime?> ("ApprovedDate");

                b.Property<string> ("Artist");

                b.Property<string> ("Creator");

                b.Property<long> ("Favourites");

                b.Property<int> ("Genre");

                b.Property<bool> ("HasVideo");

                b.Property<int> ("Language");

                b.Property<DateTime?> ("LastChecked");

                b.Property<DateTime?> ("LastUpdate");

                b.Property<int> ("RankedStatus");

                b.Property<string> ("Source");

                b.Property<string> ("Tags");

                b.Property<string> ("Title");

                b.HasKey ("SetId");

                b.ToTable ("BeatmapSet");
            });

            modelBuilder.Entity ("Pisstaube.Database.Models.ChildrenBeatmap", b =>
            {
                b.Property<int> ("BeatmapId")
                    .ValueGeneratedOnAdd ( );

                b.Property<float> ("Ar");

                b.Property<float> ("Bpm");

                b.Property<float> ("Cs");

                b.Property<string> ("DiffName");

                b.Property<double> ("DifficultyRating");

                b.Property<string> ("FileMd5");

                b.Property<long> ("HitLength");

                b.Property<float> ("Hp");

                b.Property<long> ("MaxCombo");

                b.Property<int> ("Mode");

                b.Property<float> ("Od");

                b.Property<int> ("ParentSetId");

                b.Property<int> ("Passcount");

                b.Property<int> ("Playcount");

                b.Property<int> ("TotalLength");

                b.HasKey ("BeatmapId");

                b.HasIndex ("ParentSetId");

                b.ToTable ("Beatmaps");
            });

            modelBuilder.Entity ("Pisstaube.Database.Models.ChildrenBeatmap", b =>
            {
                b.HasOne ("Pisstaube.Database.Models.BeatmapSet", "Parent")
                    .WithMany ("ChildrenBeatmaps")
                    .HasForeignKey ("ParentSetId")
                    .OnDelete (DeleteBehavior.Cascade);
            });
#pragma warning restore 612, 618
        }
    }
}
