using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using Pisstaube.Allocation;
using Pisstaube.Database;
using Pisstaube.Engine;
using Pisstaube.Enums;
using StatsdClient;

namespace Pisstaube.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly BeatmapSearchEngine _searchEngine;
        private readonly Cache _cache;

        public SearchController(BeatmapSearchEngine searchEngine, Cache cache)
        {
            _searchEngine = searchEngine;
            _cache = cache;
        }

        // GET /api/search
        [HttpGet]
        public ActionResult<string> Get(
            [FromQuery] string query = "",
            [FromQuery] int amount = 100,
            [FromQuery] int offset = 0,
            [FromQuery] BeatmapSetOnlineStatus? status = null,
            [FromQuery] PlayMode mode = PlayMode.All
        )
        {
            var raw = Request.Query.ContainsKey("raw");
            var ha = query + amount + offset + status + mode;
            
            if (_cache.TryGet(ha, out string ca))
                return ca;
            
            var result = _searchEngine.Search(query, amount, offset, status, mode);
            
            DogStatsd.Increment("beatmap.searches");
            
            if (result.Count == 0) result = null; // Cheesegull logic ^^,
            
            if (!raw)
                ca = JsonConvert.SerializeObject(result, Formatting.None, new JsonSerializerSettings
                {
                    FloatFormatHandling = FloatFormatHandling.DefaultValue
                });
            else
            {
                if (result == null)
                {
                    ca = "-1\nNo Beatmaps were found!";
                    
                    goto Return;
                }

                ca = string.Empty;
                
                ca += result.Count >= 100 ? "101" : result.Count.ToString();

                ca += "\n";

                foreach (var set in result)
                {
                    ca += set.ToDirect();
                }
            }
            
            Return:
            _cache.Set(ha, ca, TimeSpan.FromMinutes(10));
            return ca;
        }
    }
}