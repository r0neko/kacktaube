using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using StatsdClient;

namespace Pisstaube.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class HashController : ControllerBase
    {
        private readonly PisstaubeDbContext _dbContext;
        private object _dbContextLock = new object();

        public HashController(PisstaubeDbContext dbContext) => _dbContext = dbContext;

        // GET /api/v1/hash
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/v1/hash/:FileMd5
        [HttpGet("{hash}")]
        public ActionResult<BeatmapSet> Get(string hash)
        {
            DogStatsd.Increment("v1.beatmap.hash");
            lock (_dbContextLock) {
                var bm = _dbContext.Beatmaps.FirstOrDefault(cb => cb.FileMd5 == hash);
                if (bm == null)
                    return null;

                var set = _dbContext.BeatmapSet.FirstOrDefault(s => s.SetId == bm.ParentSetId);
                if (set == null)
                    return null;

                set.ChildrenBeatmaps = _dbContext.Beatmaps.Where(cb => cb.ParentSetId == set.SetId).ToList();
                
                return set;
            }
        }
    }
}