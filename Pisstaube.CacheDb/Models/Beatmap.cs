using System.ComponentModel.DataAnnotations;
using osu.Game.IO;

namespace Pisstaube.CacheDb.Models
{
    public class Beatmap
    {
        [Key]
        [Required]
        public int BeatmapId { get; set; }
        public string Hash { get; set; }
        public string FileMd5 { get; set; }
    }
}