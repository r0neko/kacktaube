using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Database.Migrations
{
    public partial class BPM_as_Double : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Bpm",
                table: "Beatmaps",
                nullable: false,
                oldClrType: typeof(float));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "Bpm",
                table: "Beatmaps",
                nullable: false,
                oldClrType: typeof(double));
        }
    }
}
