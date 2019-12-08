using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;


namespace SpleeterAPI.Youtube
{
    public class YoutubeProcessor
    {
        private class StemFileInfo
        {
            public string FileName { get; set; }
            public string FileNameWithoutExtension { get; set; }
            public string FilePath { get; set; }
        }
        private static int Max_Duration_Seconds = int.Parse(Startup.Configuration["Youtube:MaxDuration"]);
        private static string Output_Root = Startup.Configuration["Spleeter:OutputFolder"];
        private static string Cache_Root = Startup.Configuration["Spleeter:CacheFolder"];
        private readonly ILogger<YoutubeProcessor> _logger;
        private readonly static ConcurrentDictionary<string, DateTime> _processing = new ConcurrentDictionary<string, DateTime>();
        private readonly IHttpContextAccessor _httpContextAccessor;
        public YoutubeProcessor(ILogger<YoutubeProcessor> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public ProcessResponse Process(YoutubeProcessRequest request)
        {
            var mainSw = Stopwatch.StartNew();
            var logEntry = new YoutubeOutputLogEntry()
            {
                StartTime = DateTime.Now,
                Vid = request.Vid,
                Config = $"{request.BaseFormat}{(request.Options.IncludeHighFrequencies ? "-hf" : "")}{request.Extension}",
                IpAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString(),
                Cache = "Miss"
            };
            // 0. Check output cache
            var outputFilename = GetOutputFileName(request.Vid, request.BaseFormat, request.SubFormats, request.Extension, request.Options.IncludeHighFrequencies);
            var outputFilePath = $"{Output_Root}/yt/{outputFilename}";
            if (File.Exists(outputFilePath))
            {
                logEntry.Cache = "L1 HIT";
                logEntry.Success = true;
                logEntry.TimeToProcess = (int)mainSw.Elapsed.TotalSeconds;
                _logger.LogInformation($"Output cache hit: {outputFilePath}");
                return new ProcessResponse() { FileId = outputFilename, LogEntry = logEntry };
            }

            // Check processing cache, avoid duplicate requests to run
            var now = DateTime.UtcNow;
            if (_processing.TryGetValue(outputFilename, out DateTime startDate))
            {
                var startedSecondsAgo = (now - startDate).TotalSeconds;
                if (startedSecondsAgo < 1800)
                {
                    return new ProcessResponse() { Error = $"File {outputFilename} is being processed, started {startedSecondsAgo:N0} seconds ago. Try again later in few more minutes..." };
                }
            }
            _processing[outputFilename] = now;

            // 1. Get video title and duration
            var info = YoutubeHelper.GetVideoInfo(request.Vid);
            if (info.DurationSeconds > Max_Duration_Seconds)
            {
                return new ProcessResponse() { Error = $"Cannot process videos longer than {Max_Duration_Seconds} seconds" };
            }
            logEntry.Title = info.Filename;
            LogStart(request, info);

            // 2. Download Audio
            var audio = YoutubeHelper.DownloadAudio(request.Vid);

            // 3. Split
            var sw = Stopwatch.StartNew();
            var splitOutputFolder = GetAudioSplitOutputFolder(request);
            int fileCount = Directory.Exists(splitOutputFolder) ? Directory.GetFiles(splitOutputFolder, "*.mp3", SearchOption.AllDirectories).Length : 0;
            if (fileCount == int.Parse(request.BaseFormat.Substring(0, 1)))
            {
                logEntry.Cache = "L2 HIT";
                _logger.LogInformation("Split output cache hit");
            }
            else
            {
                if (fileCount > 0)
                {
                    _logger.LogInformation($"Deleting folder {splitOutputFolder}");
                    Directory.Delete(splitOutputFolder, true);
                }
                // Create the directory to avoid File exists error on spleeter separate process (probably because of missing exist_ok=True parameter in https://github.com/deezer/spleeter/blob/42d476f6fa8d06f498389d1e620d2f1f59565f51/spleeter/audio/ffmpeg.py#L108)
                Directory.CreateDirectory(Path.Combine(splitOutputFolder, request.Vid));
                // Execute the split
                var splitResult = SpliterHelper.Split(audio.AudioFileFullPath, splitOutputFolder, request, isBatch: false);
                _logger.LogInformation($"Separation for {request.Vid}: {(splitResult.ExitCode == 0 ? "Successful" : "Failed")}\n\tDuration: {info.Duration}\n\tProcessing time: {sw.Elapsed:hh\\:mm\\:ss}");
                logEntry.TimeToSeparate = (int)sw.Elapsed.TotalSeconds;
                logEntry.Success = splitResult.ExitCode == 0;
                logEntry.Errors = string.Join(", ", splitResult.Errors);
                if (splitResult.ExitCode != 0)
                {
                    _processing.TryRemove(outputFilename, out _);
                    return new ProcessResponse() { Error = $"spleeter separate command exited with code {splitResult.ExitCode}\nMessages: {splitResult.Output}.", LogEntry = logEntry };
                }
            }
            sw.Stop();

            // 4. Make output
            MakeOutput(audio.AudioFileFullPath, outputFilePath, splitOutputFolder, request);
            
            _processing.TryRemove(outputFilename, out _);
            logEntry.Success = true;
            return new ProcessResponse()              
            {
                FileId = outputFilename,	
                Speed = sw.Elapsed.TotalSeconds == 0 ? null : $"{info.DurationSeconds / sw.Elapsed.TotalSeconds:N1}x",	
                TotalTime = sw.Elapsed.ToString("hh\\:mm\\:ss"),
                LogEntry = logEntry
            };
        }

        /// <summary>
        /// Gets the final output filename for the request.
        /// Format: {vid}.{format}.{subformat}.{options}.{extension}
        /// </summary>
        public string GetOutputFileName(string vid, string baseFormat, IEnumerable<string> subFormats, string extension, bool includeHf)
        {
            // 
            var filename = $"{vid}.{baseFormat}";
            if (subFormats != null && subFormats.Count() > 0)
            {
                filename += $".{string.Join("", subFormats.Select(f => f.Substring(0, 1)).OrderBy(f => f))}";
            }
            if (includeHf)
            {
                filename += ".hf";
            }
            filename += extension;
            return filename;
        }

        /// <summary>
        /// Gets the output filename for the spleeter separation.
        /// Format: {cache_root}/yt/split/{vid}.{options}/{format}
        /// </summary>
        private static string GetAudioSplitOutputFolder(YoutubeProcessRequest request)
        {
            var folder = $"{Cache_Root}/yt/split/{request.Vid}";
            if (request.Options.IncludeHighFrequencies)
            {
                folder += ".hf";
            }
            folder += $"/{request.BaseFormat}";
            return folder;
        }

        private void MakeOutput(string originalAudioFile, string outputFilePath, string splitOutputFolder, YoutubeProcessRequest request)
        {
            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
            var stemFiles = Directory.GetFiles(splitOutputFolder, "*.mp3", SearchOption.AllDirectories)
                    .Select(sf => new StemFileInfo { FileName = Path.GetFileName(sf), FileNameWithoutExtension = Path.GetFileNameWithoutExtension(sf), FilePath = sf })
                    .ToList();
            
            if (request.Extension == ".mp4")
            {
                // Video
                var video = YoutubeHelper.DownloadVideo(request.Vid, true);
                string audioFilepath;
                if (request.SubFormats != null && request.SubFormats.Count == 1)
                {
                    // Single audio file for the video
                    audioFilepath = stemFiles.FirstOrDefault(sf => sf.FileNameWithoutExtension == request.SubFormats[0])?.FilePath;
                }
                else
                {
                    // Multiple audio files for the video
                    audioFilepath = outputFilePath.Replace(".mp4", ".mp3");
                    MergeMp3(audioFilepath, stemFiles.Where(sf => request.SubFormats.Contains(sf.FileNameWithoutExtension)));
                }

                if (audioFilepath != null)
                {
                    var cmd = $"ffmpeg -y -i \"{video.VideoFileFullPath}\" -i \"{audioFilepath}\" -c:v copy -c:s mov_text -map 0:v:0 -map 1:a:0 -map 0:s:0? \"{outputFilePath}\"";
                    var shellResult = ShellHelper.Execute(cmd);
                    if (shellResult.ExitCode != 0)
                    {
                        throw new Exception(shellResult.Output);
                    }
                }
            }
            else if (request.Extension == ".mp3")
            {
                // Return a single stem mp3 or merge multiple mp3s
                MakeMp3(outputFilePath, request, stemFiles);
            }
            else if (request.Extension == ".zip")
            {
                // ZIP multiple MP3s
                MakeZip(originalAudioFile, outputFilePath, request, stemFiles);
            }
        }

        private void MakeZip(string originalAudioFile, string outputFilePath, YoutubeProcessRequest request, List<StemFileInfo> stemFiles)
        {
            _logger.LogInformation($"Will create Zip file {outputFilePath}.");
            using (var zip = new FileStream(outputFilePath, FileMode.Create))
            {
                using (var archive = new ZipArchive(zip, ZipArchiveMode.Create))
                {
                    // Add the original audio file
                    archive.CreateEntryFromFile(originalAudioFile, $"original/{Path.GetFileName(originalAudioFile)}.webm");
                    // Add the stems requested
                    var validSubformats = YoutubeHelper.FormatMapSub[request.BaseFormat];
                    foreach (var subformat in validSubformats)
                    {
                        if (request.SubFormats == null || request.SubFormats.Count == 0 || request.SubFormats.Contains(subformat))
                        {
                            var stemFile = stemFiles.FirstOrDefault(sf => sf.FileNameWithoutExtension == subformat);
                            if (stemFile != null)
                            {
                                _logger.LogInformation($"Adding stem {subformat} to Zip file.");
                                archive.CreateEntryFromFile(stemFile.FilePath, stemFile.FileName);
                            }
                        }
                    }
                }
            }
        }

        private void MakeMp3(string outputFilePath, YoutubeProcessRequest request, List<StemFileInfo> stemFiles)
        {
            if (request.SubFormats != null && request.SubFormats.Count == 1)
            {
                // Single MP3
                _logger.LogInformation($"Will copy {request.SubFormats[0]}.mp3 stem to {outputFilePath}.");
                var stemFile = stemFiles.FirstOrDefault(sf => sf.FileNameWithoutExtension == request.SubFormats[0]);
                if (stemFile != null)
                {
                    File.Copy(stemFile.FilePath, outputFilePath, true);
                }
                else
                {
                    _logger.LogError($"Stem file {request.SubFormats[0]} not found for {request.Vid}");
                }
            }
            else
            {
                // Multiple MP3s, merge with ffmpeg
                MergeMp3(outputFilePath, stemFiles.Where(sf => request.SubFormats.Contains(sf.FileNameWithoutExtension)));
            }
        }

        private void MergeMp3(string outputFilePath, IEnumerable<StemFileInfo> stemFilesToMerge)
        {
            if (File.Exists(outputFilePath))
            {
                _logger.LogInformation("Merged mp3 cache hit");
                return;
            }
            var inputParams = stemFilesToMerge
                    .Select(sf => $"-i \"{sf.FilePath}\"")
                    .ToList();
            var mergeCmd = $"ffmpeg -y {string.Join(' ', inputParams)} -filter_complex \"[0:0][1:0] amix=inputs={inputParams.Count}:duration=longest\" \"{outputFilePath}\"";
            var shellResult = ShellHelper.Execute(mergeCmd);
            if  (shellResult.ExitCode != 0)
            {
                throw new Exception(shellResult.Output);
            }
        }

        private void LogStart(YoutubeProcessRequest request, YoutubeVideoInfo info)
        {
            Startup.EphemeralLog($"=== YouTube Process ===", true);
            Startup.EphemeralLog($"Request: {JsonConvert.SerializeObject(request)} - Title: {info.Filename} - Duration: {info.Duration}", true);
        }

    }
}
