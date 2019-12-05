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
using Microsoft.AspNetCore.Cors;
using Audit.WebApi;

namespace SpleeterAPI.Controllers
{

    [Route("mp3")]
    [ApiController]
    [EnableCors]
    [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = true)]
    public class Mp3Controller : ControllerBase
    {
        private static string Input_Root = Startup.Configuration["Spleeter:InputFolder"];
        private static string Output_Root = Startup.Configuration["Spleeter:OutputFolder"];
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
            if (format != "2stems" && format != "4stems" && format != "5stems")
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

            var archiveName = GetArchiveName(Request.Form.Files, format, includeHf);

            var now = DateTime.UtcNow;
            if (_processing.TryGetValue(archiveName, out DateTime startDate))
            {
                var startedSecondsAgo = (now - startDate).TotalSeconds;
                if (startedSecondsAgo < 1800)
                {
                    return new ProcessResponse() { Error = $"File {archiveName} is being processed, started {startedSecondsAgo:N0} seconds ago. Try again later in few more minutes..." };
                }
            }

            var zipFile = $"{Output_Root}/{archiveName}.zip";
            if (System.IO.File.Exists(zipFile))
            {
                return new ProcessResponse() { FileId = $"{archiveName}.zip" };
            }

            _processing[archiveName] = now;

            var inputFolder = $"{Input_Root}/{archiveName}";
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
            var separateResult = SpliterHelper.Split(inputFileParam,  $"{Output_Root}/{archiveName}", format, includeHf, isBatch: true);
            sw.Stop();
            _logger.LogInformation($"Separation for {inputFilenames.Count} files:\n\tProcessing time: {sw.Elapsed:hh\\:mm\\:ss}");

            if (separateResult.ExitCode != 0)
            {
                _processing.TryRemove(archiveName, out _);
                return Problem($"spleeter separate command exited with code {separateResult.ExitCode}\nMessages: {separateResult.Output}.");
            }

            // 3. Zip the output folder
            ZipFile.CreateFromDirectory($"{Output_Root}/{archiveName}", zipFile, CompressionLevel.Fastest, false);

            // 4. Delete temp files
            Directory.Delete($"{Output_Root}/{archiveName}", true);
            Directory.Delete(inputFolder, true);
            
            _processing.TryRemove(archiveName, out _);

            return new ProcessResponse()
            {
                FileId = $"{archiveName}.zip",
                TotalTime = sw.Elapsed.ToString("hh\\:mm\\:ss")
            };
        }


        [HttpGet("d")]
        [AuditApi(IncludeResponseHeaders = true, IncludeHeaders = true, IncludeResponseBody = false)]
        public ActionResult Download([FromQuery] string fn)
        {
            if (string.IsNullOrWhiteSpace(fn))
            {
                return BadRequest();
            }
            var fileName = Path.GetFileName(fn);
            var file = $"{Output_Root}/{fileName}";
            var cType = fileName.ToLower().EndsWith("zip") ? "application/zip" : "application/mp3";
            if (System.IO.File.Exists(file))
            {
                return PhysicalFile(file, cType, fileName);
            }
            return Problem($"File {fileName} not found");
        }

        private string GetArchiveName(IFormFileCollection files, string format, bool includeHf)
        {
            int hash1 = string.Join('|', files.OrderBy(f => f.FileName).Select(f => f.FileName).ToArray()).GetStableHashCode();
            int hash2 = (int)(files.Sum(f => f.Length) % int.MaxValue);
            var fileId = StringHelper.SeededString(hash1, 8) + StringHelper.SeededString(hash2, 8);
            fileId += $".{format}";
            if (includeHf)
            {
                fileId += ".hf";
            }
            return fileId;
        }

    }
}
