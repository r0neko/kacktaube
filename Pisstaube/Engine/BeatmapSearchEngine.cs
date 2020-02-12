using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore;
using Nest;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace Pisstaube.Engine
{
    public class BeatmapSearchEngine : IBeatmapSearchEngineProvider
    {
        private readonly PisstaubeDbContext dbContext;
        private readonly object dbContextMutex = new object();
        private readonly ElasticClient elasticClient;

        public BeatmapSearchEngine(PisstaubeDbContext dbContext)
        {
            this.dbContext = dbContext;
            
            var settings = new ConnectionSettings(
                    new Uri(
                        $"http://{Environment.GetEnvironmentVariable("ELASTIC_HOSTNAME")}:{Environment.GetEnvironmentVariable("ELASTIC_PORT")}"
                    )
                )
                .DefaultMappingFor<ElasticBeatmap>(m => m
                    .IdProperty(p => p.Id)
                )
                .RequestTimeout(new TimeSpan(0, 10, 0))
                .EnableHttpCompression()
                .DefaultIndex("pisstaube");

            elasticClient = new ElasticClient(settings);
        }

        public void Index(IEnumerable<BeatmapSet> sets)
        {
            var elasticBeatmaps = sets.Select(ElasticBeatmap.GetElasticBeatmap).ToList();

            var c = 0;
            while (c < elasticBeatmaps.Count)
            {
                var truncatedBeatmaps = elasticBeatmaps.Skip(c).Take(100_000).ToList(); // Submit beatmaps in Chunks

                // Delete if exists.
                Logger.LogPrint($"Deleting chunk {c + truncatedBeatmaps.Count} from ElasticSearch");
                elasticClient.DeleteMany(truncatedBeatmaps);
                /* var res = // Ignore errors for DeleteMany.
                if (!res.IsValid)
                    Logger.LogPrint(res.DebugInformation);
                */
                
                Logger.LogPrint($"Submitting chunk {c + truncatedBeatmaps.Count}");
                var result = elasticClient.IndexMany(elasticBeatmaps); // Index all truncated maps at once.
                if (!result.IsValid)
                    Logger.LogPrint(result.DebugInformation, LoggingTarget.Network, LogLevel.Important);
                
                c += truncatedBeatmaps.Count;
            }
        }
        
        public IEnumerable<BeatmapSet> Search(string query,
            int amount = 50,
            int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All)
        {
            if (amount > 50 || amount <= -1)
                amount = 50;
            
            var result = elasticClient.Search<ElasticBeatmap>(s =>
            {
                var ret = s
                    .From(offset)
                    .Size(amount);

                if (query == "") {
                    ret = ret
                        .MinScore(0)
                        .Aggregations(a => a.Max("ApprovedDate", f => f.Field(v => v.ApprovedDate)));
                }
                else
                {
                    ret = ret.MinScore(5);
                }
                ret = ret.Query(q =>
                            q.Bool(b => b
                                .Must(must =>
                                {
                                    QueryContainer res = must;

                                    if (rankedStatus != null)
                                    {
                                        if (rankedStatus == BeatmapSetOnlineStatus.Ranked ||
                                            rankedStatus == BeatmapSetOnlineStatus.Approved)
                                        {
                                            res = must.Term(term => term.Field(p => p.RankedStatus)
                                                .Value(BeatmapSetOnlineStatus.Approved));
                                            res |= must.Term(term => term.Field(p => p.RankedStatus)
                                                .Value(BeatmapSetOnlineStatus.Ranked));
                                        }
                                        else
                                        {
                                            res = must.Term(term => term.Field(p => p.RankedStatus)
                                                .Value(rankedStatus));
                                        }
                                    }

                                    if (mode != PlayMode.All)
                                        res &= must.Term(term => term.Field(p => p.Mode)
                                            .Value(mode));

                                    return res;
                                })
                                .Should(should =>
                                    {
                                        var res =
                                            should.Match(match => match.Field(p => p.Creator).Query(query).Boost(2)) ||
                                            should.Match(match => match.Field(p => p.Artist).Query(query).Boost(3)) ||
                                            should.Match(match => match.Field(p => p.DiffName).Query(query).Boost(.9)) ||
                                            should.Match(match => match.Field(p => p.Tags).Query(query).Boost(.9)) ||
                                            should.Match(match => match.Field(p => p.Title).Query(query).Boost(3));

                                        if (query == "")
                                            res = should;

                                        return res;
                                    }

                                )
                            )
                        );
                
                Logger.LogPrint(elasticClient.RequestResponseSerializer.SerializeToString(ret), LoggingTarget.Network, LogLevel.Debug);
                
                return ret;
            });

            if (!result.IsValid)
            {
                Logger.LogPrint(result.DebugInformation, LoggingTarget.Network, LogLevel.Important);
                return null;
            }
            
            var r = new List<BeatmapSet>();
            lock (dbContextMutex)
            {
                r.AddRange(from hit in result.Hits
                    where hit != null
                    select dbContext.BeatmapSet.Include(o => o.ChildrenBeatmaps)
                        .FirstOrDefault(o => o.SetId == hit.Source.Id));
            }
            
            var sets = new List<BeatmapSet>();
            
            foreach (var s in r)
            {
                // Fixes an Issue where osu!direct may not load!
                s.Artist = s.Artist.Replace("|", "");
                s.Title = s.Title.Replace("|", "");
                s.Creator = s.Creator.Replace("|", "");

                foreach (var bm in s.ChildrenBeatmaps)
                    bm.DiffName = bm.DiffName.Replace("|", "");
                
                sets.Add(s);
            }

            return sets;
        }
    }
}