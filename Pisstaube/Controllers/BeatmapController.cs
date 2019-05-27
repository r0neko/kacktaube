using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Controllers
{
    [Route("api")] 
    [ApiController]
    public class BeatmapController : ControllerBase
    {
        private readonly PisstaubeDbContext _context;

        public BeatmapController(PisstaubeDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get()
        {
            return null;
        }
        
        // GET /api/b/:BeatmapId
        [HttpGet("b/{beatmapId:int}")]
        public ActionResult<ChildrenBeatmap> GetBeatmap(int beatmapId) =>
            _context.Beatmaps.FirstOrDefault(cb => cb.BeatmapId == beatmapId);

        // GET /api/b/:BeatmapSetId
        [HttpGet("s/{beatmapSetId:int}")]
        public ActionResult<BeatmapSet> GetSet(int beatmapSetId)
        {
            var set = _context.BeatmapSet.FirstOrDefault(s => s.SetId == beatmapSetId);
            Console.WriteLine(beatmapSetId);
            if (set == null)
                return null;
            
            set.ChildrenBeatmaps = _context.Beatmaps.Where(cb => cb.ParentSetId == set.SetId).ToList();
            return set;
        }
    }
}