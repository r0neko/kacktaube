using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using StatsdClient;

namespace Pisstaube.Controllers
{
    [Route("api")]
    [ApiController]
    public class BeatmapController : ControllerBase
    {
        private readonly PisstaubeDbContextFactory _contextFactory;

        public BeatmapController(PisstaubeDbContextFactory contextFactory) => _contextFactory = contextFactory;

        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/b/:BeatmapId
        // GET /b/:BeatmapId
        [HttpGet("b/{beatmapId:int}")]
        [HttpGet("/b/{beatmapId:int}")]
        public ActionResult<string> GetBeatmap(int beatmapId)
        {
            DogStatsd.Increment("beatmap.request");

            var raw = Request.Query.ContainsKey("raw");
            if (!raw)
                return JsonConvert.SerializeObject(_contextFactory.Get().Beatmaps
                    .FirstOrDefault(cb => cb.BeatmapId == beatmapId));

            var set = _contextFactory.Get().Beatmaps.Where(bm => bm.BeatmapId == beatmapId).Select(
                bm => _contextFactory
                    .Get().BeatmapSet.FirstOrDefault(s => s.SetId == bm.ParentSetId)
            ).FirstOrDefault();

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

        // GET /api/b/:BeatmapIds
        // GET /b/:BeatmapIds
        [HttpGet("b/{beatmapIds}")]
        [HttpGet("/b/{beatmapIds}")]
        public ActionResult<string> GetBeatmap(string beatmapIds)
        {
            var raw = Request.Query.ContainsKey("raw");
            if (raw)
                return "raw is not supported!";

            try
            {
                var bms = beatmapIds.Split(";");
                var bmIds = Array.ConvertAll(bms, int.Parse);

                return JsonConvert.SerializeObject(
                    _contextFactory.Get().Beatmaps
                        .Where(cb => bmIds.Any(x => cb.BeatmapId == x)));
            }
            catch (FormatException)
            {
                return "parameter MUST be an int array! E.G 983680;983692;983896";
            }
        }

        // GET /api/s/:BeatmapSetId
        // GET /s/:BeatmapSetId
        [HttpGet("s/{beatmapSetId:int}")]
        [HttpGet("/s/{beatmapSetId:int}")]
        public async Task<ActionResult<string>> GetSet(int beatmapSetId)
        {
            var raw = Request.Query.ContainsKey("raw");
            var set = await
                _contextFactory.Get().BeatmapSet
                    .Where(s => s.SetId == beatmapSetId)
                    .Include(x => x.ChildrenBeatmaps)
                    .FirstOrDefaultAsync();

            DogStatsd.Increment("beatmap.set.request");

            if (!raw)
                return JsonConvert.SerializeObject(set);

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

        // GET /api/s/:BeatmapSetId
        // GET /s/:BeatmapSetId
        [HttpGet("s/{beatmapSetIds}")]
        [HttpGet("/s/{beatmapSetIds}")]
        public ActionResult<string> GetSet(string beatmapSetIds)
        {
            var raw = Request.Query.ContainsKey("raw");
            if (raw)
                return "raw is not supported!";

            try
            {
                var bms = beatmapSetIds.Split(";");
                var bmsIds = Array.ConvertAll(bms, int.Parse);

                return JsonConvert.SerializeObject(
                    _contextFactory.Get().BeatmapSet.Where(set => bmsIds.Any(s => set.SetId == s))
                        .Include(x => x.ChildrenBeatmaps)
                );
            }
            catch (FormatException)
            {
                return "parameter MUST be an int array! E.G 1;16";
            }
        }

        private struct BeatmapInfo
        {
            public string Artist;
            public string Title;
            public string Creator;
            public string Diffname; // TODO: split file name
        }

        // GET /api/f/:Beatmap File Name
        // GET /f/:Beatmap File Name
        [HttpGet("f/{bmfileName}")]
        [HttpGet("/f/{bmfileName}")]
        public async Task<ActionResult<string>> GetBeatmapSetByFile(string bmFileName)
        {
            var names = Regex.Split(bmFileName, @"(?<!\\);").Select(n => n.Replace("\\;", ";")).ToArray();

            foreach (var name in names) Console.WriteLine(name);

            // TODO: Add Cache as this is slow.
            var bms = (
                from b in _contextFactory.Get().Beatmaps
                join p in _contextFactory.Get().BeatmapSet on b.ParentSetId equals p.SetId
                where names.Any(s => s == Regex.Replace($"{p.Artist} - {p.Title} ({p.Creator}) [{b.DiffName}].osu",
                                         @"[^\u0000-\u007F]+", string.Empty)) ||
                      names.Any(s => s == Regex.Replace($"{p.Artist} - {p.Title} ({p.Creator}).osu",
                                         @"[^\u0000-\u007F]+", string.Empty))
                select p
            ).Include(s => s.ChildrenBeatmaps);

            return JsonConvert.SerializeObject(bms);
        }
    }
}