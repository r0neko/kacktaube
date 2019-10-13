using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.CacheDb.Migrations
{
    public partial class FileHashes : Migration
    {
        protected override void Up (MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string> (
                name: "Hash",
                table: "CacheBeatmapSet",
                nullable : true);

            migrationBuilder.CreateTable (
                name: "CacheBeatmaps",
                columns : table => new
                {
                    BeatmapId = table.Column<int> (nullable: false)
                        .Annotation ("Sqlite:Autoincrement", true),
                        Hash = table.Column<string> (nullable: true),
                        FileMd5 = table.Column<string> (nullable: true)
                },
                constraints : table =>
                {
                    table.PrimaryKey ("PK_CacheBeatmaps", x => x.BeatmapId);
                });
        }

        protected override void Down (MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable (
                name: "CacheBeatmaps");

            migrationBuilder.DropColumn (
                name: "Hash",
                table: "CacheBeatmapSet");
        }
    }
}
