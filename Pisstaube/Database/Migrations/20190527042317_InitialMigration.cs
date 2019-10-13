using System;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Database.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up (MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql ($@"ALTER DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");

            migrationBuilder.CreateTable (
                name: "BeatmapSet",
                columns : table => new
                {
                    SetId = table.Column<int> (nullable: false)
                        .Annotation ("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                        RankedStatus = table.Column<int> (nullable: false),
                        ApprovedDate = table.Column<DateTime> (nullable: true),
                        LastUpdate = table.Column<DateTime> (nullable: true),
                        LastChecked = table.Column<DateTime> (nullable: true),
                        Artist = table.Column<string> (nullable: true),
                        Title = table.Column<string> (nullable: true),
                        Creator = table.Column<string> (nullable: true),
                        Source = table.Column<string> (nullable: true),
                        Tags = table.Column<string> (nullable: true),
                        HasVideo = table.Column<bool> (nullable: false),
                        Genre = table.Column<int> (nullable: false),
                        Language = table.Column<int> (nullable: false),
                        Favourites = table.Column<long> (nullable: false)
                },
                constraints : table =>
                {
                    table.PrimaryKey ("PK_BeatmapSet", x => x.SetId);
                });

            migrationBuilder.CreateTable (
                name: "Beatmaps",
                columns : table => new
                {
                    BeatmapId = table.Column<int> (nullable: false)
                        .Annotation ("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                        ParentSetId = table.Column<int> (nullable: false),
                        DiffName = table.Column<string> (nullable: true),
                        FileMd5 = table.Column<string> (nullable: true),
                        Mode = table.Column<int> (nullable: false),
                        Bpm = table.Column<float> (nullable: false),
                        Ar = table.Column<float> (nullable: false),
                        Od = table.Column<float> (nullable: false),
                        Cs = table.Column<float> (nullable: false),
                        Hp = table.Column<float> (nullable: false),
                        TotalLength = table.Column<int> (nullable: false),
                        HitLength = table.Column<long> (nullable: false),
                        Playcount = table.Column<int> (nullable: false),
                        Passcount = table.Column<int> (nullable: false),
                        MaxCombo = table.Column<long> (nullable: false),
                        DifficultyRating = table.Column<double> (nullable: false)
                },
                constraints : table =>
                {
                    table.PrimaryKey ("PK_Beatmaps", x => x.BeatmapId);
                    table.ForeignKey (
                        name: "FK_Beatmaps_BeatmapSet_ParentSetId",
                        column : x => x.ParentSetId,
                        principalTable: "BeatmapSet",
                        principalColumn: "SetId",
                        onDelete : ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex (
                name: "IX_Beatmaps_ParentSetId",
                table: "Beatmaps",
                column: "ParentSetId");
        }

        protected override void Down (MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable (
                name: "Beatmaps");

            migrationBuilder.DropTable (
                name: "BeatmapSet");
        }
    }
}
