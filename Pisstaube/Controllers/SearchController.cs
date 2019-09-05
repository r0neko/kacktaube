using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using Pisstaube.Allocation;
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

        private bool GetTryFromQuery<T>(IEnumerable<string> keys, out T val)
        {
            var rString = string.Empty;
            
            foreach (var k in keys)
            {
                if (!Request.Query.ContainsKey(k)) continue;
                
                Request.Query.TryGetValue(k, out var x);
                rString += x;
            }

            if (rString == null)
            {
                val = default;
                return false;
            }

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (!converter.IsValid(rString))
            {
                val = default;
                return false;
            }
            
            val = (T) converter.ConvertFromString(rString);
            return true;
        }

        private int RuriStatus(int status)
        {
            switch (status)
            {
                case 0:
                    return 1;
                case 2:
                    return 0;
                case 3:
                    return 3;
                case 4:
                    return -100;
                case 5:
                    return -2;
                case 7:
                    return 2;
                case 8:
                    return 4;
                default:
                    return 1;
            }
        }
        
        // GET /api/search
        [HttpGet]
        public ActionResult<string> Get()
        {
            var raw = Request.Query.ContainsKey("raw");
            
            GetTryFromQuery(new[] {"query",   "q"}, out string query);
            GetTryFromQuery(new[] {"amount",  "a"}, out int amount);
            GetTryFromQuery(new[] {"offset",  "o"}, out int offset);
            GetTryFromQuery(new[] {"page",    "p"}, out int page);
            GetTryFromQuery(new[] {"mode",    "m"}, out PlayMode mode);
            GetTryFromQuery(new[] {"status",  "r"}, out int? r);
            GetTryFromQuery(new[] {"ruri",    "ru"}, out bool ruri);

            BeatmapSetOnlineStatus? status = null;
            if (r != null && ruri)
                status = (BeatmapSetOnlineStatus) RuriStatus(r.Value);
            else if (r != null)
                status = (BeatmapSetOnlineStatus) r.Value;

            offset += 100 * page;
            
            if (query.ToLower().Equals("newest") ||
                query.ToLower().Equals("top rated") ||
                query.ToLower().Equals("most played"))
                query = "";

            var ha = query + amount + offset + status + mode + page + raw;

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