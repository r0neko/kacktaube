using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using opi.v1;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly BeatmapSearchEngine _searchEngine;

        public SearchController(BeatmapSearchEngine searchEngine)
        {
            _searchEngine = searchEngine;
        }
        
        // GET /api/search
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get(
            [FromQuery] string query = "",
            [FromQuery] int amount = 100,
            [FromQuery] int offset = 0,
            [FromQuery] RankedStatus? status = null,
            [FromQuery] PlayMode mode = PlayMode.All
            )
        {
            var result = _searchEngine.Search(query, amount, offset, status, mode);

            if (result.Count == 0) result = null; // Cheesegull logic ^^,
            
            return result;
        }

    }
}