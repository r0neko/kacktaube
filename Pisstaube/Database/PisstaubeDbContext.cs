using System;

using Microsoft.EntityFrameworkCore;

using Pisstaube.Database.Models;

using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Pisstaube.Database
{
    public class PisstaubeDbContext : DbContext
    {
        public DbSet<BeatmapSet> BeatmapSet { get; set; }
        public DbSet<ChildrenBeatmap> Beatmaps { get; set; }

        private static readonly bool[ ] Migrated = { false };
        public PisstaubeDbContext ( )
        {
            if (Migrated[0]) return;
            lock (Migrated)
            {
                if (Migrated[0]) return;
                Database.Migrate ( );
                Migrated[0] = true;
            }
        }

        protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring (optionsBuilder);

            var Host = Environment.GetEnvironmentVariable ("MARIADB_HOST");
            var Port = Environment.GetEnvironmentVariable ("MARIADB_PORT");
            var Username = Environment.GetEnvironmentVariable ("MARIADB_USERNAME");
            var Password = Environment.GetEnvironmentVariable ("MARIADB_PASSWORD");
            var Db = Environment.GetEnvironmentVariable ("MARIADB_DATABASE");

            optionsBuilder.UseMySql (
                $"Server={Host};Database={Db};User={Username};Password={Password};Port={Port};CharSet=utf8mb4;SslMode=none;",
                mysqlOptions =>
                {
                    mysqlOptions.ServerVersion (new Version (10, 2, 15), ServerType.MariaDb);
                    mysqlOptions.UnicodeCharSet (CharSet.Utf8mb4);
                    mysqlOptions.AnsiCharSet (CharSet.Utf8mb4);
                }
            );
        }

        protected override void OnModelCreating (ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BeatmapSet> ( )
                .HasMany (j => j.ChildrenBeatmaps)
                .WithOne (j => j.Parent)
                .HasForeignKey (j => j.ParentSetId);
        }
    }
}
