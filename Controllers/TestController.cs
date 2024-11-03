using Audit.WebApi;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpleeterAPI.Split;
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
    [AuditApi]
    public class TestController : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public TestController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        [HttpGet]
        public string Get()
        {
            var ip = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
            ip = ip.Replace("::ffff:", "");
            var geo = GeoLocationHelper.GetGeoLocation(ip);
            var moduleFile = Process.GetCurrentProcess().MainModule.FileName;
            var lastModified = System.IO.File.GetLastWriteTime(moduleFile);
            var x = JsonSerializer.Serialize(new
            {
                Environment.MachineName,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                OSDescription = RuntimeInformation.OSDescription.ToString(),
                BuildDate = lastModified,
                Environment.ProcessorCount,
                ClientIp = ip,
                ClientGeo = geo
            });
            return x;

        }
    }
}
