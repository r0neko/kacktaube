using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class HashController : ControllerBase
    {
        private readonly PisstaubeDbContext dbContext;
        private object dbContextLock = new object();

        public HashController(PisstaubeDbContext dbContext) => this.dbContext = dbContext;

        // GET /api/v1/hash
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/v1/hash/:FileMd5
        [HttpGet("{hash}")]
        public ActionResult<BeatmapSet> Get(string hash)
        {
            lock (dbContextLock) {
                var bm = dbContext.Beatmaps.FirstOrDefault(cb => cb.FileMd5 == hash);
                if (bm == null)
                    return null;

                var set = dbContext.BeatmapSet.FirstOrDefault(s => s.SetId == bm.ParentSetId);
                if (set == null)
                    return null;

                set.ChildrenBeatmaps = dbContext.Beatmaps.Where(cb => cb.ParentSetId == set.SetId).ToList();
                return set;
            }
        }
    }
}