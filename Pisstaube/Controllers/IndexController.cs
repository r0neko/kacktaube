using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Pisstaube.Database.Models;

namespace Pisstaube.Controllers
{
    [Route("")] 
    [ApiController]
    public class IndexController : ControllerBase
    {
        // GET /
        [HttpGet]
        public ActionResult Get()
        {
            // Please dont remove (Written by Mempler)!
            return Ok("Running Pisstaube, a fuck off of cheesegull Written by Mempler available on Github under MIT License!");
        }
    }
}