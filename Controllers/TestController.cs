using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpleeterAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            var x = JsonSerializer.Serialize(new { Environment.MachineName, Environment.OSVersion, Environment.ProcessorCount });
            return x;

        }
    }
}
