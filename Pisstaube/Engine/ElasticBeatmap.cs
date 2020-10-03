using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Nest;
using osu.Game.Beatmaps;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Engine
{
    [ElasticsearchType(IdProperty = nameof(Id))]
    public class ElasticBeatmap
    {
        private readonly PisstaubeDbContext _dbContext;
        public int Id;
        public BeatmapSetOnlineStatus RankedStatus;

        public string Artist;
        public string Title;
        public string Creator;
        public List<string> Tags;
        public List<PlayMode> Mode;
        public List<string> DiffName;

        public double ApprovedDate;

        public override string ToString() =>
            $"\nSetId: {Id}\n" +
            $"RankedStatus: {RankedStatus}\n" +
            $"Artist: {Artist}\n" +
            $"Title: {Title}\n" +
            $"Creator: {Creator}\n" +
            $"Tags: [{Tags.Join(", ")}]\n" +
            $"Mode: {Mode}\n" +
            $"DiffName: {DiffName}\n" +
            $"ApprovedDate: {ApprovedDate}";

        public int TotalPlays
        {
            get
            {
                var x = _dbContext.BeatmapSet.Find(Id).ChildrenBeatmaps.OrderByDescending(x => x.Playcount).ToList();
                if(x != null && x.Count > 0) return x[0].Playcount;
                return 0;
            }
        }

        public static ElasticBeatmap GetElasticBeatmap(BeatmapSet bmSet)
        {
            var bm = new ElasticBeatmap
            {
                Id = bmSet.SetId,
                Artist = bmSet.Artist,
                Creator = bmSet.Creator,
                RankedStatus = bmSet.RankedStatus,
                Mode = bmSet.ChildrenBeatmaps.Select(cb => cb.Mode).ToList(),
                Tags = bmSet.Tags.Split(" ").Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                Title = bmSet.Title,
                DiffName = bmSet.ChildrenBeatmaps.Select(cb => cb.DiffName).ToList(),
                ApprovedDate =
                    bmSet.ApprovedDate?.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds ?? 0
            };
            return bm;
        }
    }
}