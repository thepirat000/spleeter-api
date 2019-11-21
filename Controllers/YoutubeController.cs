using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SpleeterAPI.Youtube;

namespace SpleeterAPI.Controllers
{

    [Route("yt")]
    [ApiController]
    public class YoutubeController : ControllerBase
    {
        private readonly ILogger<YoutubeController> _logger;
        private readonly static ConcurrentDictionary<string, DateTime> _processing = new ConcurrentDictionary<string, DateTime>();
        private static int Max_Duration_Seconds = int.Parse(Startup.Configuration["Youtube:MaxDuration"]);

        public YoutubeController(ILogger<YoutubeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Process a youtube video to split the audio 
        /// </summary>
        /// <param name="vid">Youtube video ID</param>
        /// <param name="format">2stems, 4stems or 5stems</param>
        [HttpGet("p/{format}/{vid}")]
        [Produces("application/json")]
        public ActionResult<ProcessResponse> Process([FromRoute] string format, [FromRoute] string vid, [FromQuery] bool includeOriginalAudio = false, [FromQuery] bool hf = false)
        {
            if (format != "2stems" && format != "4stems" && format != "5stems" && format != "karaoke" && format != "vocals")
            {
                return BadRequest("Format must be '2stems', '4stems' or '5stems'");
            }

            // Get video title and duration
            var info = YoutubeHelper.GetVideoInfo(vid);
            if (info.DurationSeconds == 0)
            {
                return new ProcessResponse() { Error = $"Cannot process live videos" };
            }
            if (info.DurationSeconds > Max_Duration_Seconds)
            {
                return new ProcessResponse() { Error = $"Cannot process videos longer than {Max_Duration_Seconds} seconds" };
            }

            // Set the file name
            var fileId = GetFileId(info.Filename, format, includeOriginalAudio, hf);
            var now = DateTime.UtcNow;

            // Check cache
            if (_processing.TryGetValue(fileId, out DateTime startDate))
            {
                var startedSecondsAgo = (now - startDate).TotalSeconds;
                if (startedSecondsAgo < 1800)
                {
                    return new ProcessResponse() { Error = $"File {fileId} is being processed, started {startedSecondsAgo:N0} seconds ago. Try again later in few more minutes..." };
                }
            }
            var zipFile = $"/output/{fileId}.zip";
            if (format == "karaoke" || format == "vocals")
            {
                var mp3File = $"/output/{fileId}.mp3";
                if (System.IO.File.Exists(mp3File))
                {
                    return new ProcessResponse() { FileId = fileId };
                }
            }
            else
            {
                if (System.IO.File.Exists(zipFile))
                {
                    return new ProcessResponse() { FileId = fileId };
                }
            }

            _processing[fileId] = now;

            // Download audio from youtube vid
            var audioData = YoutubeHelper.DownloadAudio(vid, fileId);

            // Separate audio stems
            var sw = Stopwatch.StartNew();
            var separateResult = SpliterHelper.Split(audioData.AudioFileFullPath, fileId, format, hf, _logger);
            _logger.LogInformation($"Separation for {fileId}:\n\tDuration: {info.Duration}\n\tProcessing time: {sw.Elapsed:hh\\:mm\\:ss}");

            if (separateResult.ExitCode != 0)
            {
                _processing.TryRemove(fileId, out _);
                return Problem($"spleeter separate command exited with code {separateResult.ExitCode}\nException: {separateResult.Exception}\nMessages: {separateResult.Output}.");
            }

            if ((format != "karaoke" && format != "vocals") && includeOriginalAudio)
            {
                System.IO.File.Copy(audioData.AudioFileFullPath, $"/output/{fileId}/original.webm", true);
            }

            if (format == "karaoke")
            {
                System.IO.File.Copy($"/output/{fileId}/{vid}/accompaniment.mp3", $"/output/{fileId}.mp3", true);
                // Also copy the vocals
                System.IO.File.Copy($"/output/{fileId}/{vid}/vocals.mp3", $"/output/{fileId.Replace(".karaoke", ".vocals")}.mp3", true);
                // Also zip the 2stems to the output folder, to avoid processing again if 2stems is requested
                var zipFile2stems = zipFile.Replace(".karaoke", ".2stems");
                if (!System.IO.File.Exists(zipFile2stems))
                {
                    ZipFile.CreateFromDirectory($"/output/{fileId}", zipFile2stems, CompressionLevel.Fastest, false);
                }
            }
            else if (format == "vocals")
            {
                System.IO.File.Copy($"/output/{fileId}/{vid}/vocals.mp3", $"/output/{fileId}.mp3", true);
                // Also copy the karaoke
                System.IO.File.Copy($"/output/{fileId}/{vid}/accompaniment.mp3", $"/output/{fileId.Replace(".vocals", ".karaoke")}.mp3", true);
                // Also zip the 2stems to the output folder, to avoid processing again if 2stems is requested
                var zipFile2stems = zipFile.Replace(".vocals", ".2stems");
                if (!System.IO.File.Exists(zipFile2stems))
                {
                    ZipFile.CreateFromDirectory($"/output/{fileId}", zipFile2stems, CompressionLevel.Fastest, false);
                }
            }
            else
            {
                // Zip stems
                if (!System.IO.File.Exists(zipFile))
                {
                    ZipFile.CreateFromDirectory($"/output/{fileId}", zipFile, CompressionLevel.Fastest, false);
                }
                if (format == "2stems")
                {
                    // Also copy the karaoke & vocals mp3s to the output, to avoid processing again 
                    System.IO.File.Copy($"/output/{fileId}/{vid}/accompaniment.mp3", $"/output/{fileId.Replace(".2stems", ".karaoke")}.mp3", true);
                    System.IO.File.Copy($"/output/{fileId}/{vid}/vocals.mp3", $"/output/{fileId.Replace(".2stems", ".vocals")}.mp3", true);
                }
            }

            // Delete temp files
            System.IO.Directory.Delete($"/output/{fileId}", true);

            _processing.TryRemove(fileId, out _);

            return new ProcessResponse() { FileId = fileId };
        }

        /// <summary>
        /// Downloads an already processed youtube video
        /// </summary>
        /// <param name="vid">Youtube video ID</param>
        /// <param name="format">2stems, 4stems or 5stems</param>
        [HttpGet("d/{format}/{vid}")]
        public ActionResult Download([FromRoute] string format, [FromRoute] string vid, [FromQuery] bool includeOriginalAudio = false, [FromQuery] bool hf = false)
        {
            var info = YoutubeHelper.GetVideoInfo(vid);
            var fileId = GetFileId(info.Filename, format, includeOriginalAudio, hf);
            if (format == "karaoke" || format == "vocals")
            {
                var mp3File = $"/output/{fileId}.mp3";
                if (System.IO.File.Exists(mp3File))
                {
                    return PhysicalFile(mp3File, "audio/mpeg", $"{fileId}.mp3");
                }
            }
            var zipFile = $"/output/{fileId}.zip";
            if (System.IO.File.Exists(zipFile))
            {
                return PhysicalFile(zipFile, "application/zip", $"{fileId}.zip");
            }
            return Problem($"File {zipFile} not found");
        }

        /// <summary>
        /// Query the status of a youtube video being splitted
        /// </summary>
        /// <param name="vid">Youtube video ID</param>
        /// <param name="format">2stems, 4stems or 5stems</param>
        [HttpGet("q/{format}/{vid}")]
        [Produces("application/json")]
        public ActionResult<YoutubeStatusResponse> Query([FromRoute] string format, [FromRoute] string vid, [FromQuery] bool includeOriginalAudio = false, [FromQuery] bool hf = false)
        {
            var info = YoutubeHelper.GetVideoInfo(vid);
            var fileId = GetFileId(info.Filename, format, includeOriginalAudio, hf);
            var result = new YoutubeStatusResponse() { FileId = fileId };
            if (_processing.TryGetValue(fileId, out DateTime startDate))
            {
                result.StartDate = startDate;
            }
            if (format == "karaoke" || format == "vocals")
            {
                var mp3File = $"/output/{fileId}.mp3";
                if (System.IO.File.Exists(mp3File))
                {
                    result.Status = "ReadyToDownload";
                }
                else
                {
                    result.Status = "Unknown";
                }
            }
            else
            {
                var zipFile = $"/output/{fileId}.zip";
                if (System.IO.File.Exists(zipFile))
                {
                    result.Status = "ReadyToDownload";
                }
                else if (YoutubeHelper.AudioExists(vid))
                {
                    result.Status = "Splitting";
                }
                else
                {
                    result.Status = "Unknown";
                }
            }
            return result;
        }

        private string GetFileId(string title, string format, bool includeOriginalAudio, bool includeHighFreq)
        {
            var fileId = $"{title}.{format}";
            if (includeOriginalAudio || includeHighFreq)
            {
                fileId += ".";
                if (includeHighFreq)
                {
                    fileId += "hf";
                }
                if (includeOriginalAudio)
                {
                    fileId += "o";
                }
            }
            return fileId;
        }
    }
}
