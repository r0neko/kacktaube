using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;

namespace Pisstaube.Database.Models
{
    public class ChildrenBeatmap
    {
        [Key]
        [Required]
        [JsonProperty("BeatmapID")]
        public int BeatmapId { get; set; }

        [JsonProperty("ParentSetID")]
        public int ParentSetId { get; set; }
        
        [JsonIgnore]
        public BeatmapSet Parent { get; set; } 

        [JsonProperty("DiffName")]
        public string DiffName { get; set; }

        [JsonProperty("FileMD5")]
        public string FileMd5 { get; set; }

        [JsonProperty("Mode")]
        public PlayMode Mode { get; set; }

        [JsonProperty("BPM")]
        public float Bpm { get; set; }

        [JsonProperty("AR")]
        public float Ar { get; set; }

        [JsonProperty("OD")]
        public float Od { get; set; }

        [JsonProperty("CS")]
        public float Cs { get; set; }

        [JsonProperty("HP")]
        public float Hp { get; set; }

        [JsonProperty("TotalLength")]
        public int TotalLength { get; set; }

        [JsonProperty("HitLength")]
        public long HitLength { get; set; }

        [JsonProperty("Playcount")]
        public int Playcount { get; set; }

        [JsonProperty("Passcount")]
        public int Passcount { get; set; }

        [JsonProperty("MaxCombo")]
        public long MaxCombo { get; set; }

        [JsonProperty("DifficultyRating")]
        public double DifficultyRating { get; set; }

        public static ChildrenBeatmap FromBeatmapInfo(BeatmapInfo info, BeatmapSet parent = null)
        {
            if (info == null)
                return null;

            var cb = new ChildrenBeatmap();

            cb.BeatmapId = info.OnlineBeatmapID ?? -1;
            cb.ParentSetId = info.BeatmapSetInfoID;
            cb.Parent = parent;
            cb.DiffName = info.Version;
            cb.FileMd5 = info.MD5Hash;
            cb.Mode = (PlayMode) info.RulesetID;
            cb.Ar = info.BaseDifficulty.ApproachRate;
            cb.Od = info.BaseDifficulty.OverallDifficulty;
            cb.Cs = info.BaseDifficulty.CircleSize;
            cb.Hp = info.BaseDifficulty.DrainRate;
            cb.TotalLength = (int) info.OnlineInfo.Length;
            cb.HitLength = (int) info.StackLeniency; // TODO: check
            cb.Playcount = info.OnlineInfo.PassCount;
            cb.Playcount = info.OnlineInfo.PlayCount;
            cb.MaxCombo = 0; // TODO: Fix
            cb.DifficultyRating = info.StarDifficulty;

            return cb;
        }
    }
}