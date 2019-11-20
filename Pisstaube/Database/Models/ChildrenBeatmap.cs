using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using Pisstaube.Enums;
using Pisstaube.Utils;

namespace Pisstaube.Database.Models
{
    [Serializable]
    public class ChildrenBeatmap : ISerializer
    {
        [Key]
        [Required]
        [DataMember(Name = "BeatmapID")]
        public int BeatmapId { get; set; }

        [DataMember(Name = "ParentSetID")]
        public int ParentSetId { get; set; }
        
        [IgnoreDataMember]
        [ForeignKey(nameof(ParentSetId))]
        public BeatmapSet Parent { get; set; }
        
        [DataMember(Name = "DiffName")]
        public string DiffName { get; set; }
        
        [DataMember(Name = "FileMD5")]
        public string FileMd5 { get; set; }

        [DataMember(Name = "Mode")]
        public PlayMode Mode { get; set; }
        
        [DataMember(Name = "BPM")]
        public double Bpm { get; set; }
        
        [DataMember(Name = "AR")]
        public float Ar { get; set; }

        [DataMember(Name = "OD")]
        public float Od { get; set; }

        [DataMember(Name = "CS")]
        public float Cs { get; set; }

        [DataMember(Name = "HP")]
        public float Hp { get; set; }
        
        [DataMember(Name = "TotalLength")]
        public int TotalLength { get; set; }

        [DataMember(Name = "HitLength")]
        public long HitLength { get; set; }

        [DataMember(Name = "Playcount")]
        public int Playcount { get; set; }

        [DataMember(Name = "Passcount")]
        public int Passcount { get; set; }

        [DataMember(Name = "MaxCombo")]
        public long MaxCombo { get; set; }

        [DataMember(Name = "DifficultyRating")]
        public double DifficultyRating { get; set; }

        public static ChildrenBeatmap FromBeatmapInfo(BeatmapInfo info, BeatmapSetOnlineInfo setOnlineInfo,
            BeatmapSet parent = null)
        {
            if (info == null)
                return null;

            var cb = new ChildrenBeatmap
            {
                BeatmapId = info.OnlineBeatmapID ?? -1,
                ParentSetId = info.BeatmapSetInfoID,
                Parent = parent,
                DiffName = info.Version,
                FileMd5 = info.MD5Hash,
                Mode = (PlayMode) info.RulesetID,
                Ar = info.BaseDifficulty.ApproachRate,
                Od = info.BaseDifficulty.OverallDifficulty,
                Cs = info.BaseDifficulty.CircleSize,
                Hp = info.BaseDifficulty.DrainRate,
                TotalLength = info.OnlineInfo.CircleCount + info.OnlineInfo.SliderCount,
                HitLength = (int) info.StackLeniency,
                Playcount = info.OnlineInfo.PassCount,
                Bpm = setOnlineInfo.BPM,
                MaxCombo = info.OnlineInfo.CircleCount,
                DifficultyRating = info.StarDifficulty
            };

            return cb;
        }

        public string ToDirect() => $"{DiffName.Replace("@", "")} " +
                                    $"({Math.Round(DifficultyRating, 2)}★~" +
                                    $"{Bpm}♫~AR" +
                                    $"{Ar}~OD" +
                                    $"{Od}~CS" +
                                    $"{Cs}~HP" +
                                    $"{Hp}~" +
                                    $"{(int) MathF.Floor(TotalLength) / 60}m" +
                                    $"{TotalLength % 60}s)@" +
                                    $"{(int) Mode},";

        public void ReadFromStream(MStreamReader sr)
        {
            BeatmapId = sr.ReadInt32();
            ParentSetId = sr.ReadInt32();
            DiffName = sr.ReadString();
            FileMd5 = sr.ReadString();
            Mode = (PlayMode) sr.ReadSByte();
            Bpm = sr.ReadInt32();
            Ar = sr.ReadSingle();
            Od = sr.ReadSingle();
            Cs = sr.ReadSingle();
            Hp = sr.ReadSingle();
            TotalLength = sr.ReadInt32();
            Playcount = sr.ReadInt32();
            Passcount = sr.ReadInt32();
            MaxCombo = sr.ReadInt64();
            DifficultyRating = sr.ReadDouble();
        }

        public void WriteToStream(MStreamWriter sw)
        {
            sw.Write(BeatmapId);
            sw.Write(ParentSetId);
            sw.Write(DiffName, true);
            sw.Write(FileMd5, true);
            sw.Write((sbyte) Mode);
            sw.Write(Bpm);
            sw.Write(Ar);
            sw.Write(Od);
            sw.Write(Cs);
            sw.Write(Hp);
            sw.Write(TotalLength);
            sw.Write(Playcount);
            sw.Write(Passcount);
            sw.Write(MaxCombo);
            sw.Write(DifficultyRating);
        }
    }
}