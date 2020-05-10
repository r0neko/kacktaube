using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Nest;
using Pisstaube.Database.Models;

namespace Pisstaube.Engine
{
    [ElasticsearchType(IdProperty = nameof(Id))]
    public class ElasticBeatmap
    {
        public string Id;
        public string RankedStatus;

        public string Artist;
        public string Title;
        public string Creator;
        public List<string> Tags;
        public List<string> Mode;
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

        public static ElasticBeatmap GetElasticBeatmap(BeatmapSet bmSet)
        {
            var bm = new ElasticBeatmap
            {
                Id = bmSet.SetId.ToString(),
                Artist = bmSet.Artist,
                Creator = bmSet.Creator,
                RankedStatus = bmSet.RankedStatus.ToString(),
                Mode = bmSet.ChildrenBeatmaps.Select(cb => ((int) cb.Mode).ToString()).ToList(),
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