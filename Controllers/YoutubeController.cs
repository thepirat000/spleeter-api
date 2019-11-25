using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Audit.WebApi;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SpleeterAPI.Youtube;

namespace SpleeterAPI.Controllers
{

    [Route("yt")]
    [ApiController]
    [EnableCors]
    [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = true)]
    public class YoutubeController : ControllerBase
    {
        private readonly ILogger<YoutubeController> _logger;
        private readonly static ConcurrentDictionary<string, DateTime> _processing = new ConcurrentDictionary<string, DateTime>();
        private static int Max_Duration_Seconds = int.Parse(Startup.Configuration["Youtube:MaxDuration"]);
        private static string Output_Root = Startup.Configuration["Spleeter:OutputFolder"];
        private readonly YoutubeProcessor _processor;

        public YoutubeController(ILogger<YoutubeController> logger, YoutubeProcessor processor)
        {
            _logger = logger;
            _processor = processor;
        }

        /// <summary>
        /// Process a youtube video for audio splitting
        /// </summary>
        [HttpPost("p")]
        [Produces("application/json")]
        public ActionResult<ProcessResponse> Process_V2([FromBody]YoutubeProcessRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }
            if (!ValidateVid(request.Vid))
            {
                return BadRequest($"'{request.Vid}' is not a valid video ID");
            }
            if (!ValidateFormat(request.BaseFormat, request.SubFormats))
            {
                return BadRequest($"'{request.BaseFormat}:{string.Join("+", request.SubFormats)}' is not a valid format");
            }
            if (!ValidateExtension(request.Extension))
            {
                return BadRequest($"'{request.Extension}' is not a valid extension");
            }
            if (request.Options == null)
            {
                request.Options = new YoutubeProcessOptions();
            }

            var result = _processor.Process(request);

            return result;
        }

        /// <summary>
        /// Downloads an already processed youtube video
        /// </summary>
        /// <param name="vid">Youtube video ID</param>
        /// <param name="format">2stems, 4stems or 5stems</param>
        [HttpGet("d/{format}/{vid}")]
        [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = false)]
        public ActionResult Download([FromRoute] string format, [FromRoute] string vid, [FromQuery] string sub, [FromQuery] string ext, [FromQuery] bool hf = false)
        {
            if (!ValidateVid(vid))
            {
                return BadRequest($"'{vid}' is not a valid video ID");
            }
            var subFormats = sub?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            if (subFormats != null && !ValidateFormat(format, subFormats))
            {
                return BadRequest($"'{format}:{string.Join("+", subFormats)}' is not a valid format");
            }
            if (!ValidateExtension(ext))
            {
                return BadRequest($"'{ext}' is not a valid extension");
            }

            var outputFilename = _processor.GetOutputFileName(vid, format, subFormats, ext, hf);
            var outputFilePath = $"{Output_Root}/yt/{outputFilename}";
            if (System.IO.File.Exists(outputFilePath))
            {
                var info = YoutubeHelper.GetVideoInfo(vid);
                var contentType = ext == ".mp4" ? "video/mp4" : ext == ".zip" ? "application/zip" : "audio/mpeg";
                return PhysicalFile(outputFilePath, contentType, info.Filename + ext);
            }
            return Problem($"File {outputFilename} not found");
        }

        [HttpGet("dd/{vid}")]
        [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = false)]
        public ActionResult DirectDownload([FromRoute] string vid)
        {
            var info = YoutubeHelper.GetVideoInfo(vid);
            if (info.DurationSeconds > (Max_Duration_Seconds * 2))
            {
                return BadRequest($"Cannot process videos longer than {Max_Duration_Seconds * 2} seconds");
            }
            var video = YoutubeHelper.DownloadVideo(vid, true);
            if (System.IO.File.Exists(video.VideoFileFullPath))
            {
                return PhysicalFile(video.VideoFileFullPath, "video/mp4", $"{System.IO.Path.GetFileName(video.VideoFileFullPath)}");
            }
            return BadRequest("Video requested was not found");
        }

        private string GetArchiveName(string title, string format, bool includeHighFreq, bool isVideo)
        {
            var fileId = $"{title}.{format}";
            if (includeHighFreq)
            {
                fileId += ".h";
            }
            return fileId;
        }

        private string FixFormat(string format, out string extension)
        {
            extension = format.EndsWith("_v") ? "mp4" : format.EndsWith("stems") ? "zip" : "mp3";
            format = format.EndsWith("_v") ? format[0..^2] : format;
            if (format != "2stems" && format != "4stems" && format != "5stems" && format != "karaoke" && format != "vocals")
            {
                throw new ArgumentException("Format must be '2stems', '4stems' or '5stems'");
            }
            return format;
        }

        private bool ValidateVid(string vid)
        {
            if (vid == null)
            {
                return false;
            }
            return YoutubeHelper.ValidVidRegex.IsMatch(vid);
        }

        private bool ValidateFormat(string baseFormat, List<string> subFormats)
        {
            if (baseFormat == null)
            {
                return false;
            }
            if (!YoutubeHelper.FormatMapSub.ContainsKey(baseFormat))
            {
                return false;
            }
            if (subFormats == null || subFormats.Count == 0)
            {
                return true;
            }
            if (subFormats.Count != subFormats.Distinct().Count())
            {
                return false;
            }
            if (!subFormats.All(sf => YoutubeHelper.FormatMapSub[baseFormat].Contains(sf)))
            {
                return false;
            }
            return true;
        }

        private bool ValidateExtension(string extension)
        {
            if (extension == null || (extension != ".mp3" && extension != ".mp4" && extension != ".zip") )
            {
                return false;
            }
            return true;
        }

    }

}
