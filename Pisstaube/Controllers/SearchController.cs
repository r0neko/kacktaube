using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;
using Pisstaube.Database;
using Sora.Allocation;
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

            string ca;
            if ((ca = _cache.Get<string>(ha)) != null)
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