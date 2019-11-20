using System;
using Microsoft.EntityFrameworkCore;
using Pisstaube.Database.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Storage;

namespace Pisstaube.Database
{
    public class PisstaubeDbContext : DbContext
    {
        public DbSet<BeatmapSet> BeatmapSet { get; set; }
        public DbSet<ChildrenBeatmap> Beatmaps { get; set; }

        private static readonly bool[] Migrated = {false};

        public PisstaubeDbContext()
        {
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

            var host = Environment.GetEnvironmentVariable("MARIADB_HOST");
            var port = Environment.GetEnvironmentVariable("MARIADB_PORT");
            var username = Environment.GetEnvironmentVariable("MARIADB_USERNAME");
            var password = Environment.GetEnvironmentVariable("MARIADB_PASSWORD");
            var db = Environment.GetEnvironmentVariable("MARIADB_DATABASE");

            optionsBuilder.UseMySql(
                $"Server={host};Database={db};User={username};Password={password};Port={port};CharSet=utf8mb4;SslMode=none;",
                mysqlOptions =>
                {
                    mysqlOptions.ServerVersion(new Version(10, 2, 15), ServerType.MariaDb);
                    mysqlOptions.CharSet(CharSet.Utf8Mb4);
                }
            );
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