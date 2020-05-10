using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using Pisstaube.Database;
using Pisstaube.Engine;

namespace Pisstaube.DatabaseRotator
{
    // a little tool to rotate the upstream database with ElasticSearch
    internal static class Program
    {
        private static PisstaubeDbContext _dbContext = new PisstaubeDbContext();
        private static BeatmapSearchEngine _searchEngine = new BeatmapSearchEngine(_dbContext);
        
        private static async Task Main(string[] args)
        {
            Logger.Level = LogLevel.Debug;
            
            while (!_searchEngine.IsConnected)
            {
                Logger.LogPrint("Search Engine is not yet Connected!", LoggingTarget.Database, LogLevel.Important);
                Thread.Sleep(1000);
            }
            
            Logger.LogPrint("Fetching all beatmap sets...");
            var beatmapSets = await _dbContext.BeatmapSet
                .Include(o => o.ChildrenBeatmaps)
                .ToListAsync();
            Logger.LogPrint($"{beatmapSets.Count} Beatmap sets to index.", LoggingTarget.Database);
            
            _searchEngine.Index(beatmapSets);
        }
    }
}