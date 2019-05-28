using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using Pisstaube.Database;
using Pisstaube.Utils;

namespace Pisstaube.Controllers
{
    public enum RecoveryAction
    {
        RepairElastic,
        RecrawlEverything,
        RecrawlUnknown
    }
    
    [Route("api/pisstaube")] 
    [ApiController]
    public class PrivateAPIController : ControllerBase
    {
        // GET /api/pisstaube/dump?key={PRIVATE_API_KEY}
        [HttpGet("dump")]
        public ActionResult DumpDatabase([FromServices] PisstaubeDbContext db, [FromQuery] string key)
        {
            // TODO: Finish
            return NotFound();
        }

        [HttpGet("recovery")]
        public ActionResult Recovery(
            [FromServices] PisstaubeDbContext db,
            [FromServices] BeatmapSearchEngine searchEngine,
            [FromServices] Crawler crawler,
            [FromQuery] string key,
            [FromQuery] RecoveryAction action
            )
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");

            switch (action)
            {
                case RecoveryAction.RepairElastic:
                    new Thread(() =>
                    {
                        Logger.LogPrint("Repairing ElasticSearch");
                        crawler.Stop();

                        searchEngine.DeleteAllBeatmaps();
                    
                        foreach (var beatmapSet in db.BeatmapSet)
                        {
                            beatmapSet.ChildrenBeatmaps = db.Beatmaps.Where(b => b.ParentSetId == beatmapSet.SetId).ToList();
                            searchEngine.IndexBeatmap(beatmapSet);
                        }
                    
                        if (Environment.GetEnvironmentVariable("CRAWLER_DISABLED") != "true")
                            crawler.BeginCrawling();
                    }).Start();
                    break;
                case RecoveryAction.RecrawlEverything:
                    new Thread(() =>
                    {
                        Logger.LogPrint("Recrawl Everything!");
                    
                        crawler.Stop();
                        searchEngine.DeleteAllBeatmaps();
                        db.Database.ExecuteSqlCommand("SET FOREIGN_KEY_CHECKS = 0;" +
                                                      "TRUNCATE TABLE `Beatmaps`;" +
                                                      "ALTER TABLE `Beatmaps` AUTO_INCREMENT = 1;" +
                                                      "TRUNCATE TABLE `BeatmapSet`;" +
                                                      "ALTER TABLE `BeatmapSet` AUTO_INCREMENT = 1;" +
                                                      "SET FOREIGN_KEY_CHECKS = 1;");
                        crawler.BeginCrawling();
                    }).Start();
                    break;
                case RecoveryAction.RecrawlUnknown:
                    new Thread(() =>
                    {
                        Logger.LogPrint("Recrawl All unknown maps!");
                    
                        crawler.Stop();
                        for (var i = 0; i < db.BeatmapSet.Last().SetId; i++)
                        {
                            if (!db.BeatmapSet.Any(set => set.SetId == i))
                                crawler.Crawl(i, db);
                        }
                        crawler.BeginCrawling();
                    }).Start();
                    break;
                default:
                    return BadRequest("Unknown Action type!");
            }

            return Ok("Success!");
        }
    }
}