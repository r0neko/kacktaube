using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Database.Migrations
{
    public partial class DisabledBeatmaps : Migration
    {
        protected override void Up (MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool> (
                name: "Disabled",
                table: "BeatmapSet",
                nullable : false,
                defaultValue : false);
        }

        protected override void Down (MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn (
                name: "Disabled",
                table: "BeatmapSet");
        }
    }
}
