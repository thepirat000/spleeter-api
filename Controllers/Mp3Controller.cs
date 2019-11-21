using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SpleeterAPI.Youtube;
using SpleeterAPI.Split;
using System.Collections.Concurrent;

namespace SpleeterAPI.Controllers
{

    [Route("mp3")]
    [ApiController]
    public class Mp3Controller : ControllerBase
    {
        private static long Max_Upload_Size = long.Parse(Startup.Configuration["Mp3:MaxUploadSize"]);
        private readonly static ConcurrentDictionary<string, DateTime> _processing = new ConcurrentDictionary<string, DateTime>();

        private readonly ILogger<Mp3Controller> _logger;
        public Mp3Controller(ILogger<Mp3Controller> logger)
        {
            _logger = logger;
        }

        [HttpPost("p")]
        [Produces("application/json")]
        public async Task<ActionResult<ProcessResponse>> Process([FromForm] string format, [FromForm] bool includeHf)
        {
            if (format != "2stems" && format != "4stems" && format != "5stems" && format != "karaoke" && format != "vocals")
            {
                return BadRequest("Format must be '2stems', '4stems' or '5stems'");
            }
            if (Request.Form.Files?.Count == 0)
            {
                return BadRequest("No files to process");
            }
            var totalBytes = Request.Form.Files.Sum(f => f.Length);
            if (totalBytes > Max_Upload_Size)
            {
                return BadRequest($"Can't process more than {Max_Upload_Size / 1024:N0} Mb of data");
            }

            var archiveName = GetFileId(Request.Form.Files, format, includeHf, false);

            var now = DateTime.UtcNow;
            if (_processing.TryGetValue(archiveName, out DateTime startDate))
            {
                var startedSecondsAgo = (now - startDate).TotalSeconds;
                if (startedSecondsAgo < 1800)
                {
                    return new ProcessResponse() { Error = $"File {archiveName} is being processed, started {startedSecondsAgo:N0} seconds ago. Try again later in few more minutes..." };
                }
            }

            if (format == "karaoke" || format == "vocals")
            {
                if (Request.Form.Files.Count == 1)
                {
                    var mp3File = $"/output/{archiveName}.mp3";
                    if (System.IO.File.Exists(mp3File))
                    {
                        return new ProcessResponse() { FileId = $"{archiveName}.mp3" };
                    }
                }
            }

            var zipFile = $"/output/{archiveName}.zip";
            if (System.IO.File.Exists(zipFile))
            {
                return new ProcessResponse() { FileId = $"{archiveName}.zip" };
            }

            _processing[archiveName] = now;

            var inputFolder = $"/input/{archiveName}";
            if (!Directory.Exists(inputFolder))
            {
                Directory.CreateDirectory(inputFolder);
            }
            var inputFilenames = new List<string>();

            // 1. copy input files to a temp folder
            foreach (var file in Request.Form.Files)
            {
                var fileName = ShellHelper.SanitizeFilename(file.FileName);
                inputFilenames.Add($"{fileName}");
                var filePath = $"{inputFolder}/{fileName}";
                if (!System.IO.File.Exists(filePath))
                {
                    using (var output = System.IO.File.Create(filePath))
                    {
                        await file.CopyToAsync(output);
                    }
                }
            }
            if (inputFilenames.Count == 0)
            {
                _processing.TryRemove(archiveName, out _);
                return Problem("Unknown problem when creating files");
            }

            // 2. call spleeter with those multiple files
            var sw = Stopwatch.StartNew();
            var inputFileParam = string.Join(' ', inputFilenames.Select(fn => $"\"{inputFolder}/{fn}\""));
            var separateResult = SpliterHelper.Split(inputFileParam, archiveName, format, includeHf, _logger, isBatch: true);
            _logger.LogInformation($"Separation for {inputFilenames.Count} files:\n\tProcessing time: {sw.Elapsed:hh\\:mm\\:ss}");

            if (separateResult.ExitCode != 0)
            {
                _processing.TryRemove(archiveName, out _);
                return Problem($"spleeter separate command exited with code {separateResult.ExitCode}\nException: {separateResult.Exception}\nMessages: {separateResult.Output}.");
            }

            // 2.1 If karaoke
            if (format == "karaoke" || format == "vocals")
            {
                // 2.1.1 If just 1 file -> copy to output renaming as karaoke and return mp3 file name
                if (inputFilenames.Count == 1)
                {
                    var fileToCopy = format == "karaoke" ? "accompaniment.mp3" : "vocals.mp3";
                    System.IO.File.Copy($"/output/{archiveName}/{Path.GetFileNameWithoutExtension(inputFilenames[0])}/{fileToCopy}", $"/output/{archiveName}.mp3", true);
                    Directory.Delete($"/output/{archiveName}", true);
                    Directory.Delete(inputFolder, true);
                    _processing.TryRemove(archiveName, out _);
                    return new ProcessResponse() { FileId = $"{archiveName}.mp3" };
                } 
                else
                {
                    // More than 1 karaoke -> remove all the vocals.mp3
                    var fileToRemove = format == "karaoke" ? "vocals.mp3" : "accompaniment.mp3";
                    foreach (var file in Directory.EnumerateFiles($"/output/{archiveName}", fileToRemove, SearchOption.AllDirectories))
                    {
                        System.IO.File.Delete(file);
                    }
                }
            }

            // 3. Zip the output folder
            ZipFile.CreateFromDirectory($"/output/{archiveName}", zipFile, CompressionLevel.Fastest, false);

            // 4. Delete temp files
            Directory.Delete($"/output/{archiveName}", true);
            Directory.Delete(inputFolder, true);
            
            _processing.TryRemove(archiveName, out _);

            return new ProcessResponse() { FileId = $"{archiveName}.zip" };
        }


        [HttpGet("d")]
        [Produces("application/json")]
        public ActionResult Download([FromQuery] string fn)
        {
            if (string.IsNullOrWhiteSpace(fn))
            {
                return BadRequest();
            }
            var file = $"/output/{fn}";
            var cType = fn.ToLower().EndsWith("zip") ? "application/zip" : "application/mp3";
            if (System.IO.File.Exists(file))
            {
                return PhysicalFile(file, cType, fn);
            }
            return Problem($"File {fn} not found");
        }

        private string GetFileId(IFormFileCollection files, string format, bool includeHf, bool includeOri)
        {
            int hash1 = string.Join('|', files.OrderBy(f => f.FileName).Select(f => f.FileName).ToArray()).GetStableHashCode();
            int hash2 = (int)(files.Sum(f => f.Length) % int.MaxValue);
            var fileId = StringHelper.SeededString(hash1, 8) + StringHelper.SeededString(hash2, 8);
            fileId += $".{format}";
            if (includeOri || includeHf)
            {
                fileId += ".";
                if (includeHf)
                {
                    fileId += "hf";
                }
                if (includeOri)
                {
                    fileId += "o";
                }
            }
            return fileId;
        }

    }
}
