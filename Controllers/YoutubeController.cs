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
        private static string Output_Root = Startup.Configuration["Spleeter:OutputFolder"];

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
        public ActionResult<ProcessResponse> Process([FromRoute] string format, [FromRoute] string vid, [FromQuery] bool hf = false)
        {
            format = FixFormat(format, out string extension);
            
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
            var archiveName = GetArchiveName(info.Filename, format, hf, extension == "mp4");
            var now = DateTime.UtcNow;

            // Check cache
            if (_processing.TryGetValue(archiveName, out DateTime startDate))
            {
                var startedSecondsAgo = (now - startDate).TotalSeconds;
                if (startedSecondsAgo < 1800)
                {
                    return new ProcessResponse() { Error = $"File {archiveName} is being processed, started {startedSecondsAgo:N0} seconds ago. Try again later in few more minutes..." };
                }
            }

            var outFile = $"{Output_Root}/{archiveName}.{extension}";

            // Check if output file is already on fs
            if (System.IO.File.Exists(outFile))
            {
                return new ProcessResponse() { FileId = archiveName };
            }

            _processing[archiveName] = now;

            // Download audio from youtube vid
            var audioData = YoutubeHelper.DownloadAudio(vid, archiveName);

            // Separate audio stems
            var sw = Stopwatch.StartNew();
            var separateResult = SpliterHelper.Split(audioData.AudioFileFullPath, $"{Output_Root}/{archiveName}", format, hf, isBatch: false);
            _logger.LogInformation($"Separation for {archiveName}:\n\tDuration: {info.Duration}\n\tProcessing time: {sw.Elapsed:hh\\:mm\\:ss}");

            if (separateResult.ExitCode != 0)
            {
                _processing.TryRemove(archiveName, out _);
                return Problem($"spleeter separate command exited with code {separateResult.ExitCode}\nMessages: {separateResult.Output}.");
            }

            if (extension == "zip" || extension == "mp3")
            {
                // include the original audio
                System.IO.File.Copy(audioData.AudioFileFullPath, $"{Output_Root}/{archiveName}/original.webm", true);
            }

            if (format == "karaoke")
            {
                System.IO.File.Copy($"{Output_Root}/{archiveName}/{vid}/accompaniment.mp3", $"{Output_Root}/{archiveName}.mp3", true);
                // Also copy the vocals
                System.IO.File.Copy($"{Output_Root}/{archiveName}/{vid}/vocals.mp3", $"{Output_Root}/{archiveName.Replace(".karaoke", ".vocals")}.mp3", true);
                // Also zip the 2stems to the output folder, to avoid processing again if 2stems is requested
                var zipFile2stems = $"{Output_Root}/{this.GetArchiveName(info.Filename, "2stems", hf, false)}.zip";
                if (!System.IO.File.Exists(zipFile2stems))
                {
                    ZipFile.CreateFromDirectory($"{Output_Root}/{archiveName}", zipFile2stems, CompressionLevel.Fastest, false);
                }
                if (extension == "mp4")
                {
                    // Video merge
                    MakeVideo(vid, $"{Output_Root}/{archiveName}/{vid}/accompaniment.mp3", $"{Output_Root}/{archiveName}.mp4");
                }
            }
            else if (format == "vocals")
            {
                System.IO.File.Copy($"{Output_Root}/{archiveName}/{vid}/vocals.mp3", $"{Output_Root}/{archiveName}.mp3", true);
                // Also copy the karaoke
                System.IO.File.Copy($"{Output_Root}/{archiveName}/{vid}/accompaniment.mp3", $"{Output_Root}/{archiveName.Replace(".vocals", ".karaoke")}.mp3", true);
                // Also zip the 2stems to the output folder, to avoid processing again if 2stems is requested
                var zipFile2stems = $"{Output_Root}/{this.GetArchiveName(info.Filename, "2stems", hf, false)}.zip";
                if (!System.IO.File.Exists(zipFile2stems))
                {
                    ZipFile.CreateFromDirectory($"{Output_Root}/{archiveName}", zipFile2stems, CompressionLevel.Fastest, false);
                }
                if (extension == "mp4")
                {
                    // Video merge
                    MakeVideo(vid, $"{Output_Root}/{archiveName}/{vid}/vocals.mp3", $"{Output_Root}/{archiveName}.mp4");
                }
            }
            else
            {
                // Zip stems
                if (!System.IO.File.Exists(outFile))
                {
                    ZipFile.CreateFromDirectory($"{Output_Root}/{archiveName}", outFile, CompressionLevel.Fastest, false);
                }
                if (format == "2stems")
                {
                    // Also copy the karaoke & vocals mp3s to the output, to avoid processing again 
                    System.IO.File.Copy($"{Output_Root}/{archiveName}/{vid}/accompaniment.mp3", $"{Output_Root}/{archiveName.Replace(".2stems", ".karaoke")}.mp3", true);
                    System.IO.File.Copy($"{Output_Root}/{archiveName}/{vid}/vocals.mp3", $"{Output_Root}/{archiveName.Replace(".2stems", ".vocals")}.mp3", true);
                }
            }

            // Delete temp files
            System.IO.Directory.Delete($"{Output_Root}/{archiveName}", true);

            _processing.TryRemove(archiveName, out _);

            return new ProcessResponse() { FileId = archiveName };
        }

        /// <summary>
        /// Downloads an already processed youtube video
        /// </summary>
        /// <param name="vid">Youtube video ID</param>
        /// <param name="format">2stems, 4stems or 5stems</param>
        [HttpGet("d/{format}/{vid}")]
        public ActionResult Download([FromRoute] string format, [FromRoute] string vid, [FromQuery] bool hf = false)
        {
            format = FixFormat(format, out string extension);
            var info = YoutubeHelper.GetVideoInfo(vid);
            var fileId = GetArchiveName(info.Filename, format, hf, extension == "mp4");
            var outFile = $"{Output_Root}/{fileId}.{extension}";
            if (System.IO.File.Exists(outFile))
            {
                var contentType = extension == "mp4" ? "video/mp4" : extension == "zip" ? "application/zip" : "audio/mpeg";
                return PhysicalFile(outFile, contentType, $"{fileId}.{extension}");
            }
            return Problem($"File {fileId} not found");
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
            extension = format.EndsWith("+") ? "mp4" : format.EndsWith("stems") ? "zip" : "mp3";
            format = format.TrimEnd('+');
            if (format != "2stems" && format != "4stems" && format != "5stems" && format != "karaoke" && format != "vocals")
            {
                throw new ArgumentException("Format must be '2stems', '4stems' or '5stems'");
            }
            return format;
        }

        private void MakeVideo(string vid, string audioFilepath, string outputFilepath)
        {
            var video = YoutubeHelper.DownloadVideo(vid);
            var cmd = $"ffmpeg -i \"{video.VideoFileFullPath}\" -i \"{audioFilepath}\" -c:v copy -map 0:v:0 -map 1:a:0 \"{outputFilepath}\"";
            var shellResult = ShellHelper.Execute(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"ffmpeg exited with code {shellResult.ExitCode}.\n{shellResult.Output}");
            }
            if (!System.IO.File.Exists(outputFilepath))
            {
                throw new Exception($"Video filename {outputFilepath} not found after ffmpeg");
            }
        }
    }
}
