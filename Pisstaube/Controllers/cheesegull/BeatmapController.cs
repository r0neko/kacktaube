using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Utils;

namespace Pisstaube.Controllers.cheesegull
{
    [Route("api/cheesegull")]
    [ApiController]
    public class BeatmapController : ControllerBase
    {
        private readonly PisstaubeDbContext dbContext;
        private object dbContextLock = new object();

        public BeatmapController(PisstaubeDbContext dbContext) => this.dbContext = dbContext;

        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/cheesegull/b/:BeatmapId
        [HttpGet("b/{beatmapId:int}")]
        public ActionResult<string> GetBeatmap(int beatmapId)
        {
            //DogStatsd.Increment("beatmap.request");

            lock (dbContextLock) {
                var raw = Request.Query.ContainsKey("raw");
                if (!raw)
                    return JsonUtil.Serialize(dbContext.Beatmaps.FirstOrDefault(cb => cb.BeatmapId == beatmapId));

                // TODO: tried to make both in one query, results in crash. have to take a closer look in the near future.
                var beatmap = dbContext.Beatmaps.FirstOrDefault(bm => bm.BeatmapId == beatmapId);
                var set = dbContext.BeatmapSet.FirstOrDefault(s => s.SetId == beatmap.ParentSetId);
                
                if (set == null)
                    return "0";
                
                return $"{set.SetId}.osz|" +
                       $"{set.Artist}|" +
                       $"{set.Title}|" +
                       $"{set.Creator}|" +
                       $"{(int) set.RankedStatus}|" +
                       "10.00|" +
                       $"{set.LastUpdate}|" +
                       $"{set.SetId}|" +
                       $"{set.SetId}|" +
                       $"{Convert.ToInt32(set.HasVideo)}|" +
                       "0|" +
                       "1234|" +
                       $"{Convert.ToInt32(set.HasVideo) * 4321}\r\n";
            }
        }

        // GET /api/cheesegull/b/:BeatmapIds
        [HttpGet("b/{beatmapIds}")]
        public ActionResult<string> GetBeatmap(string beatmapIds)
        {
            var raw = Request.Query.ContainsKey("raw");
            if (raw)
                return "raw is not supported!";

            try
            {
                var bms = beatmapIds.Split(";");
                var bmIds = Array.ConvertAll(bms, int.Parse);

                lock (dbContextLock)
                    return JsonUtil.Serialize(
                        dbContext.Beatmaps
                                .Where(cb => bmIds.Any(x => cb.BeatmapId == x)));
            }
            catch (FormatException)
            {
                return "parameter MUST be an int array! E.G 983680;983692;983896";
            }
        }

        // GET /api/cheesegull/s/:BeatmapSetId
        [HttpGet("s/{beatmapSetId:int}")]
        public async Task<ActionResult<string>> GetSet(int beatmapSetId)
        {
            lock (dbContextLock)
            {
                var raw = Request.Query.ContainsKey("raw");
                var set =
                    dbContext.BeatmapSet
                        .Where(s => s.SetId == beatmapSetId)
                        .Include(x => x.ChildrenBeatmaps)
                        .FirstOrDefault();

                //DogStatsd.Increment("beatmap.set.request");

                if (!raw)
                    return JsonUtil.Serialize(set);

                if (set == null)
                    return "0";

                return $"{set.SetId}.osz|" +
                       $"{set.Artist}|" +
                       $"{set.Title}|" +
                       $"{set.Creator}|" +
                       $"{(int) set.RankedStatus}|" +
                       "10.00|" +
                       $"{set.LastUpdate}|" +
                       $"{set.SetId}|" +
                       $"{set.SetId}|" +
                       $"{Convert.ToInt32(set.HasVideo)}|" +
                       "0|" +
                       "1234|" +
                       $"{Convert.ToInt32(set.HasVideo) * 4321}\r\n";
            }
        }

        // GET /api/cheesegull/s/:BeatmapSetId
        [HttpGet("s/{beatmapSetIds}")]
        public ActionResult<string> GetSet(string beatmapSetIds)
        {
            var raw = Request.Query.ContainsKey("raw");
            if (raw)
                return "raw is not supported!";

            try
            {
                var bms = beatmapSetIds.Split(";");
                var bmsIds = Array.ConvertAll(bms, int.Parse);

                lock (dbContextLock)
                {
                    return JsonUtil.Serialize(
                        dbContext.BeatmapSet.Where(set => bmsIds.Any(s => set.SetId == s))
                            .Include(x => x.ChildrenBeatmaps)
                    );
                }
            }
            catch (FormatException)
            {
                return "parameter MUST be an int array! E.G 1;16";
            }
        }
        
        // GET /api/f/:Beatmap File Name
        [HttpGet("f/{bmfileName}")]
        [HttpGet("/f/{bmfileName}")]
        public async Task<ActionResult<string>> GetBeatmapSetByFile(string bmFileName)
        {
            var names = Regex.Split(bmFileName, @"(?<!\\);").Select(n => n.Replace("\\;", ";")).ToArray();

            foreach (var name in names) Console.WriteLine(name);

            // TODO: Add Cache as this is slow.
            lock (dbContext) {
                var bms = (
                    from b in dbContext.Beatmaps
                    join p in dbContext.BeatmapSet on b.ParentSetId equals p.SetId
                    where names.Any(s => s == Regex.Replace($"{p.Artist} - {p.Title} ({p.Creator}) [{b.DiffName}].osu",
                                             @"[^\u0000-\u007F]+", string.Empty)) ||
                          names.Any(s => s == Regex.Replace($"{p.Artist} - {p.Title} ({p.Creator}).osu",
                                             @"[^\u0000-\u007F]+", string.Empty))
                    select p
                ).Include(s => s.ChildrenBeatmaps);
                
                return JsonUtil.Serialize(bms);
            }
        }
    }
}