using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpleeterAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [EnableCors]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            var moduleFile = Process.GetCurrentProcess().MainModule.FileName;
            var lastModified = System.IO.File.GetLastWriteTime(moduleFile);
            var x = JsonSerializer.Serialize(new
            {
                Environment.MachineName,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                OSDescription = RuntimeInformation.OSDescription.ToString(),
                BuildDate = lastModified,
                Environment.ProcessorCount });
            return x;

        }
    }
}
