using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore.Internal;
using Nest;
using Newtonsoft.Json;
using opi.v1;
using Pisstaube.Database.Models;

namespace Pisstaube.Database
{
    public class ElasticBeatmap
    {
        public int SetId;
        public RankedStatus RankedStatus;

        public string Artist;
        public string Title;
        public string Creator;
        public List<string> Tags;
        public List<PlayMode> Mode;
        public List<string> DiffName;

        public ulong ApprovedDate;

        public override string ToString()
        {
            return $"SetId: {SetId}\n" +
                   $"RankedStatus: {RankedStatus}\n" +
                   $"Artist: {Artist}\n" +
                   $"Title: {Title}\n" +
                   $"Creator: {Creator}\n" +
                   $"Tags: [{Tags.Join()}]\n" +
                   $"Mode: {Mode}\n" +
                   $"DiffName: {DiffName}\n" +
                   $"ApprovedDate: {ApprovedDate}";
        }

        public static ElasticBeatmap GetElasticBeatmap(BeatmapSet bmset)
        {
            var bm = new ElasticBeatmap
            {
                SetId = bmset.SetId,
                Artist = bmset.Artist,
                Creator = bmset.Creator,
                RankedStatus = bmset.RankedStatus,
                Mode = bmset.ChildrenBeatmaps.Select(cb => cb.Mode).ToList(),
                Tags = bmset.Tags.Split(" ").Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                Title = bmset.Title,
                DiffName = bmset.ChildrenBeatmaps.Select(cb => cb.DiffName).ToList(),
                ApprovedDate =
                    (ulong) (bmset.ApprovedDate?.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds ?? 0)
            };
            return bm;
        }
    }
    public class BeatmapSearchEngine
    {
        private readonly PisstaubeDbContext _db;
        private readonly ElasticClient _elasticClient;
        
        public BeatmapSearchEngine(PisstaubeDbContext db)
        {
            _db = db;
            var settings = new ConnectionSettings(new Uri($"http://{Environment.GetEnvironmentVariable("ELASTIC_HOSTNAME")}:{Environment.GetEnvironmentVariable("ELASTIC_PORT")}"))
                .DefaultIndex("pisstaube");

            _elasticClient = new ElasticClient(settings);
            if (!_elasticClient.IndexExists("pisstaube").Exists)
                _elasticClient.CreateIndex("pisstaube");
        }

        public void IndexBeatmap(BeatmapSet set)
        {
            var map = ElasticBeatmap.GetElasticBeatmap(set);

            _elasticClient.IndexDocument(map);
        }

        public List<BeatmapSet> Search(string query,
            int amount = 100,
            int offset = 0,
            RankedStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All)
        {
            if (amount > 100 || amount <= -1)
                amount = 100;
            
            var sets = new List<BeatmapSet>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var result = _elasticClient.Search<ElasticBeatmap>(s =>
                {
                    var ret = s
                        .From(offset)
                        .Size(amount)
                        .MinScore(5)
                        .Query(q =>
                            q.Bool(b =>
                                b.Filter(filter =>
                                    rankedStatus != null ?
                                        filter.Term(term => term.Field("rankedStatus")
                                            .Value(rankedStatus)) :
                                        filter
                                    )
                                .Filter(filter =>
                                    mode != PlayMode.All ?
                                        filter.Term(term => term.Field(p => p.Mode)
                                            .Value(mode)) :
                                        filter
                                )
                                .Should(should =>
                                    should.Match(match => match.Field(p => p.Creator).Query(query)) ||
                                    should.Match(match => match.Field(p => p.Artist).Query(query)) ||
                                    should.Match(match => match.Field(p => p.DiffName).Query(query)) ||
                                    should.Match(match => match.Field(p => p.Tags).Query(query)) ||
                                    should.Match(match => match.Field(p => p.Title).Query(query).Boost(5))
                                )
                            )
                        );
                    return ret;
                });

                sets.AddRange(result.Hits.Select(hit =>
                {
                    var map = _db.BeatmapSet.First(set => set.SetId == hit.Source.SetId);
                    map.ChildrenBeatmaps = _db.Beatmaps.Where(cb => cb.Parent == map).ToList();
                    return map;
                }));
            }
            else
            {
                sets = _db.BeatmapSet.Where(set => (rankedStatus == null || set.RankedStatus == rankedStatus) &&
                                                   _db.Beatmaps.Where(cb => cb.ParentSetId == set.SetId)
                                                       .FirstOrDefault(cb => mode == PlayMode.All || cb.Mode == mode) != null)
                    .OrderByDescending(x => x.ApprovedDate)
                    .Skip(offset)
                    .Take(amount)
                    .ToList();

                foreach (var set in sets)
                    set.ChildrenBeatmaps = _db.Beatmaps.Where(cb => cb.Parent == set).ToList();
            }

            return sets;
        }
    }
}