using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
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
        private static int Max_Duration_Seconds = int.Parse(Startup.Configuration["Youtube:MaxDuration"]);
        private static string Output_Root = Startup.Configuration["Spleeter:OutputFolder"];
        private readonly YoutubeProcessor _processor;

        private static int LatestVideosMaxSize = 1000;
        private static List<string> _latestVideosProcessed = new List<string>();

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
        public ActionResult<ProcessResponse> Process([FromBody]YoutubeProcessRequest request)
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
            Startup.FileLog(YoutubeOutputLogEntry.GetHeader(), true);
            var result = _processor.Process(request);
            if (result.LogEntry != null)
            {
                Startup.FileLog(result.LogEntry.ToString());
                if (result.LogEntry.Success)
                {
                    _latestVideosProcessed.Add(result.LogEntry.Vid);
                    while (_latestVideosProcessed.Count > LatestVideosMaxSize)
                    {
                        _latestVideosProcessed.RemoveRange(0, _latestVideosProcessed.Count - LatestVideosMaxSize);
                    }
                }
            }
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

        /// <summary>
        /// Creates a youtube playlist with the latest 50 videos processed by any user
        /// </summary>
        [HttpGet("pl")]
        public ActionResult CreatePlaylist()
        {
            var videoIds = string.Join(",", _latestVideosProcessed.Distinct().Take(50));
            if (videoIds.Length > 0)
            {
                var url = $"https://www.youtube.com/watch_videos?video_ids={videoIds}";
                return Redirect(url);
            }

            return NoContent();
        }

        /// <summary>
        /// Direct Download video
        /// </summary>
        [HttpGet("ddv/{vid}")]
        [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = false)]
        public ActionResult DirectDownloadVideo([FromRoute] string vid)
        {
            if (!ValidateVid(vid))
            {
                return BadRequest($"'{vid}' is not a valid video ID");
            }
            var info = YoutubeHelper.GetVideoInfo(vid);
            if (info.DurationSeconds > (Max_Duration_Seconds * 2))
            {
                return BadRequest($"Cannot process videos longer than {Max_Duration_Seconds * 2} seconds");
            }
            var video = YoutubeHelper.DownloadVideo(vid, true);
            if (System.IO.File.Exists(video.VideoFileFullPath))
            {
                return PhysicalFile(video.VideoFileFullPath, "video/mp4", $"{info.Filename}-{vid}.mp4");
            }
            return BadRequest("Video requested was not found");
        }

        /// <summary>
        /// Direct Download audio
        /// </summary>
        [HttpGet("dda/{vid}")]
        [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = false)]
        public ActionResult DirectDownloadAudio([FromRoute] string vid)
        {
            if (!ValidateVid(vid))
            {
                return BadRequest($"'{vid}' is not a valid video ID");
            }

            var info = YoutubeHelper.GetVideoInfo(vid);
            if (info.DurationSeconds > (Max_Duration_Seconds * 2))
            {
                return BadRequest($"Cannot process videos longer than {Max_Duration_Seconds * 2} seconds");
            }

            var outputFilePath = $"{Output_Root}/yt/{vid}.mp3";
            if (System.IO.File.Exists(outputFilePath))
            {
                return PhysicalFile(outputFilePath, "audio/mpeg", $"{info.Filename}-{vid}.mp3");
            }

            var audio = YoutubeHelper.DownloadAudioMp3(vid);
            if (System.IO.File.Exists(audio))
            {
                return PhysicalFile(audio, "audio/mpeg", $"{info.Filename}-{vid}.mp3");
            }
            return BadRequest("Video requested was not found");
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
