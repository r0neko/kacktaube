using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using Pisstaube.Utils;

namespace Pisstaube.Database.Models
{
    public enum Genre
    {
        Any,
        Unspecified,
        Game,
        Anime,
        Rock,
        Pop,
        Other,
        Novelty,
        HipHop = 9,
        Electronic,
    }

    public enum Language
    {
        Any,
        Other,
        English,
        Japanese,
        Chinese,
        Instrumental,
        Korean,
        French,
        German,
        Swedish,
        Spanish,
        Italian
    }

    [Serializable]
    public class BeatmapSet : ISerializer
    {
        [Required]
        [Key]
        [DataMember(Name = "SetID")]
        public int SetId { get; set; }

        [DataMember(Name = "ChildrenBeatmaps")] public List<ChildrenBeatmap> ChildrenBeatmaps { get; set; }

        [DataMember(Name = "RankedStatus")] public BeatmapSetOnlineStatus RankedStatus { get; set; }

        [DataMember(Name = "ApprovedDate")] public DateTime? ApprovedDate { get; set; }

        [DataMember(Name = "LastUpdate")] public DateTime? LastUpdate { get; set; }

        [DataMember(Name = "LastChecked")] public DateTime? LastChecked { get; set; }

        [DataMember(Name = "Artist")] public string Artist { get; set; }

        [DataMember(Name = "Title")] public string Title { get; set; }

        [DataMember(Name = "Creator")] public string Creator { get; set; }

        [DataMember(Name = "Source")] public string Source { get; set; }

        [DataMember(Name = "Tags")] public string Tags { get; set; }

        [DataMember(Name = "HasVideo")] public bool HasVideo { get; set; }

        [DataMember(Name = "Genre")] public Genre Genre { get; set; }

        [DataMember(Name = "Language")] public Language Language { get; set; }

        [DataMember(Name = "Favourites")] public long Favourites { get; set; }

        [IgnoreDataMember] public bool Disabled { get; set; }

        public static BeatmapSet FromBeatmapSetInfo(BeatmapSetInfo info)
        {
            if (info?.Beatmaps == null)
                return null;

            var beatmapSet = new BeatmapSet
            {
                SetId = info.OnlineBeatmapSetID ?? -1,
                RankedStatus = info.Status,
                ApprovedDate = info.OnlineInfo.Ranked?.DateTime,
                LastUpdate = info.OnlineInfo.LastUpdated?.DateTime,
                LastChecked = DateTime.Now,
                Artist = info.Metadata.Artist,
                Title = info.Metadata.Title,
                Creator = info.Metadata.Author.Username,
                Source = info.Metadata.Source,
                Tags = info.Metadata.Tags,
                HasVideo = info.OnlineInfo.HasVideo,
                ChildrenBeatmaps = new List<ChildrenBeatmap>(),
                // Obsolete!
                Genre = Genre.Any,
                Language = Language.Any
            };

            foreach (var map in info.Beatmaps)
                beatmapSet.ChildrenBeatmaps.Add(ChildrenBeatmap.FromBeatmapInfo(map, info.OnlineInfo, beatmapSet));

            return beatmapSet;
        }

        public string ToDirect()
        {
            string retStr;

            double maxDiff = 0;

            foreach (var cbm in ChildrenBeatmaps)
                if (cbm.DifficultyRating > maxDiff)
                    maxDiff = cbm.DifficultyRating;

            maxDiff *= 1.5;

            retStr = $"{SetId}.osz|" +
                     $"{Artist}|" +
                     $"{Title}|" +
                     $"{Creator}|" +
                     $"{(int) RankedStatus}|" +
                     $"{maxDiff:0.00}|" +
                     $"{LastUpdate}Z|" +
                     $"{SetId}|" +
                     $"{SetId}|" +
                     "0|" +
                     "1234|" +
                     $"{Convert.ToInt32(HasVideo)}|" +
                     $"{Convert.ToInt32(HasVideo) * 4321}|";

            foreach (var cb in ChildrenBeatmaps)
                retStr += cb.ToDirect();

            return retStr.TrimEnd(',') + "|\r\n";
        }

        public void ReadFromStream(MStreamReader sr)
        {
            SetId = sr.ReadInt32();

            var count = sr.ReadInt32();
            ChildrenBeatmaps = new List<ChildrenBeatmap>();
            for (var i = 0; i < count; i++)
                ChildrenBeatmaps.Add(sr.ReadData<ChildrenBeatmap>());

            RankedStatus = (BeatmapSetOnlineStatus) sr.ReadSByte();

            if (DateTime.TryParse(sr.ReadString(), out var res))
                ApprovedDate = res;

            if (DateTime.TryParse(sr.ReadString(), out res))
                LastUpdate = res;

            if (DateTime.TryParse(sr.ReadString(), out res))
                LastChecked = res;

            Artist = sr.ReadString();
            Title = sr.ReadString();
            Creator = sr.ReadString();
            Source = sr.ReadString();
            Tags = sr.ReadString();
            HasVideo = sr.ReadBoolean();
            Genre = (Genre) sr.ReadSByte();
            Language = (Language) sr.ReadSByte();
            Favourites = sr.ReadInt64();
            Disabled = sr.ReadBoolean();
        }

        public void WriteToStream(MStreamWriter sw)
        {
            sw.Write(SetId);
            sw.Write(ChildrenBeatmaps.Count);
            foreach (var bm in ChildrenBeatmaps)
                sw.Write(bm);
            sw.Write((sbyte) RankedStatus);
            sw.Write(ApprovedDate?.ToString(), true);
            sw.Write(LastUpdate?.ToString(), true);
            sw.Write(LastChecked?.ToString(), true);
            sw.Write(Artist, true);
            sw.Write(Title, true);
            sw.Write(Creator, true);
            sw.Write(Source, true);
            sw.Write(Tags, true);
            sw.Write(HasVideo);
            sw.Write((sbyte) Genre);
            sw.Write((sbyte) Language);
            sw.Write(Favourites);
            sw.Write(Disabled);
        }
    }
}