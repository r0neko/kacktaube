using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using osu.Framework.Logging;
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

        private bool GetTryFromQuery<T>(IEnumerable<string> keys, T def, out T val)
        {
            var rString = string.Empty;
            
            foreach (var k in keys)
            {
                if (!Request.Query.ContainsKey(k)) continue;

                Request.Query.TryGetValue(k, out var x);
                rString += x.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(rString))
            {
                val = def;
                return false;
            }

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (!converter.IsValid(rString))
            {
                val = def;
                return false;
            }
            
            val = (T) converter.ConvertFromString(rString);
            return true;
        }
        
        // GET /api/search
        [HttpGet]
        public ActionResult<string> Get()
        {
            var raw = Request.Query.ContainsKey("raw");
            
            GetTryFromQuery(new[] {"query",   "q"}, string.Empty, out var query);
            GetTryFromQuery(new[] {"amount",  "a"}, 100, out var amount);
            GetTryFromQuery(new[] {"offset",  "o"}, 0, out var offset);
            GetTryFromQuery(new[] {"page",    "p"}, 0, out var page);
            GetTryFromQuery(new[] {"mode",    "m"}, 0,out var mode);
            GetTryFromQuery(new[] {"status",  "r"}, null, out int? r);
            GetTryFromQuery(new[] {"ruri",    "ru"}, false, out var ruri);

            BeatmapSetOnlineStatus? status = null;
            if (r != null)
                status = (BeatmapSetOnlineStatus) r.Value;

            offset += 100 * page;
            
            if (query.ToLower().Equals("newest") || 
                query.ToLower().Equals("top rated") || // TODO: Implementing this
                query.ToLower().Equals("most played")) // and this
                query = "";

            var ha = query + amount + offset + status + mode + page + raw;

            if (_cache.TryGet(ha, out string ca))
                return ca;
            
            var result = _searchEngine.Search(query, amount, offset, status, (PlayMode) mode);
            
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