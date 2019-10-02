using System;
using System.Collections.Generic;
using System.Linq;
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

        public BeatmapController(PisstaubeDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/b/:BeatmapId
        [HttpGet("b/{beatmapId:int}")]
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
                   $"{(int)set.RankedStatus}|" +
                   "10.00|" +
                   $"{set.LastUpdate}|" +
                   $"{set.SetId}|" +
                   $"{set.SetId}|" +
                   $"{Convert.ToInt32(set.HasVideo)}|" +
                   "0|" +
                   "1234|" +
                   $"{Convert.ToInt32(set.HasVideo) * 4321}\r\n";
        }

        // GET /api/b/:BeatmapSetId
        [HttpGet("s/{beatmapSetId:int}")]
        public ActionResult<string> GetSet(int beatmapSetId)
        {
            var raw = Request.Query.ContainsKey("raw");
            var set = _contextFactory.Get().BeatmapSet.FirstOrDefault(s => s.SetId == beatmapSetId);
            
            DogStatsd.Increment("beatmap.set.request");
            
            if (raw) {
                if (set == null)
                    return "0";
                
                return $"{set.SetId}.osz|" +
                       $"{set.Artist}|" +
                       $"{set.Title}|" +
                       $"{set.Creator}|" +
                       $"{(int)set.RankedStatus}|" +
                       "10.00|" +
                       $"{set.LastUpdate}|" +
                       $"{set.SetId}|" +
                       $"{set.SetId}|" +
                       $"{Convert.ToInt32(set.HasVideo)}|" +
                       "0|" +
                       "1234|" +
                       $"{Convert.ToInt32(set.HasVideo) * 4321}\r\n";
            }
            
            if (set == null) 
                return null;

            set.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(cb => cb.ParentSetId == set.SetId).ToList();
            return JsonConvert.SerializeObject(set);
        }
        
        // GET /api/f/:Beatmap File Name
        [HttpGet("f/{bmfileName}")]
        public async Task<ActionResult<string>> GetBeatmapSetByFile(string bmFileName)
        {
            var array = Request.Query.ContainsKey("array");
            
            var names = new List<string>();
            if (array)
                names.AddRange(bmFileName.Split(','));
            else
                names.Add(bmFileName);
            
            // TODO: Add Cache as this is slow.
            var bms = from b in _contextFactory.Get().Beatmaps
                join p in _contextFactory.Get().BeatmapSet on b.ParentSetId equals p.SetId
                where
                    (names.Any(s => s == $"{p.Artist} - {p.Title} ({p.Creator}) [{b.DiffName}].osu") ||
                    names.Any(s => s == $"{p.Artist} - {p.Title} ({p.Creator}).osu"))
                select p;

            if (!array) {
                var bm = await bms.FirstOrDefaultAsync();
                if (bm == null) return null;
                
                bm.ChildrenBeatmaps = await _contextFactory.Get().Beatmaps.Where(c => c.ParentSetId == bm.SetId).ToListAsync();
                
                return JsonConvert.SerializeObject(bm);
            }

            var l = await bms.ToListAsync();

            foreach (var b in l)
            {
                b.ChildrenBeatmaps = await _contextFactory.Get().Beatmaps.Where(c => c.ParentSetId == b.SetId).ToListAsync();
            }
            
            return JsonConvert.SerializeObject(bms);
        }
    }
}