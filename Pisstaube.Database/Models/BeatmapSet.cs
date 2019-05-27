using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;

namespace Pisstaube.Database.Models
{
    public class BeatmapSet
    {
        [Key]
        [Required]
        [JsonProperty("SetID")]
        public int SetId { get; set; }

        [JsonProperty("ChildrenBeatmaps")]
        public List<ChildrenBeatmap> ChildrenBeatmaps { get; set; }

        [JsonProperty("RankedStatus")]
        public RankedStatus RankedStatus { get; set; }

        [JsonProperty("ApprovedDate")]
        public DateTime? ApprovedDate { get; set; }

        [JsonProperty("LastUpdate")]
        public DateTime? LastUpdate { get; set; }

        [JsonProperty("LastChecked")]
        public DateTime? LastChecked { get; set; }

        [JsonProperty("Artist")]
        public string Artist { get; set; }

        [JsonProperty("Title")]
        public string Title { get; set; }

        [JsonProperty("Creator")]
        public string Creator { get; set; }

        [JsonProperty("Source")]
        public string Source { get; set; }

        [JsonProperty("Tags")]
        public string Tags { get; set; }

        [JsonProperty("HasVideo")]
        public bool HasVideo { get; set; }

        [JsonProperty("Genre")]
        public Genre Genre { get; set; }

        [JsonProperty("Language")]
        public Language Language { get; set; }

        [JsonProperty("Favourites")]
        public long Favourites { get; set; }
    }
}