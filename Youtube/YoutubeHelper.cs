using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace SpleeterAPI.Youtube
{
    public static class YoutubeHelper
    {
        private static string Youtube_User = Startup.Configuration["Youtube:User"];
        private static string Youtube_Pass = Startup.Configuration["Youtube:Pass"];
        private static ConcurrentDictionary<string, YoutubeVideoInfo> _videoInfoCache = new ConcurrentDictionary<string, YoutubeVideoInfo>();

        public static YoutubeVideoInfo GetVideoInfo(string vid)
        {
            if (_videoInfoCache.TryGetValue(vid, out YoutubeVideoInfo cachedInfo))
            {
                return cachedInfo;
            }
            var cmd = $"youtube-dl -s --get-filename --get-duration {vid}";
            var shellResult = ShellHelper.Bash(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl -s exited with code {shellResult.ExitCode}.\n{shellResult.Exception}\n{shellResult.Output}");
            }
            var dataArray = shellResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (dataArray.Length < 2)
            {
                throw new Exception($"youtube-dl -s returned unformatted data {shellResult.ExitCode}.\n{shellResult.Exception}\n{shellResult.Output}");
            }
            var info = new YoutubeVideoInfo()
            {
                Filename = dataArray[0].Trim(),
                Duration = dataArray[1]
            };
            if (info.Filename.Contains('.'))
            {
                info.Filename = ShellHelper.SanitizeFilename(info.Filename.Substring(0, info.Filename.LastIndexOf('.')));
            }
            var dur = info.Duration.Split(':');
            if (dur.Length == 1)
            {
                info.DurationSeconds = int.Parse(dur[0]);
            }
            else if (dur.Length == 2)
            {
                info.DurationSeconds = int.Parse(dur[0]) * 60 + int.Parse(dur[1]);
            }
            else if (dur.Length == 3)
            {
                info.DurationSeconds = int.Parse(dur[0]) * 3600 + int.Parse(dur[1]) * 60 + int.Parse(dur[2]);
            }
            _videoInfoCache[vid] = info;
            return info;
        }

        public static YoutubeAudioResponse DownloadAudio(string vid, string fileId)
        {
            var fileName = GetAudioFilename(vid);
            var result = new YoutubeAudioResponse();
            if (File.Exists(fileName))
            {
                result.AudioFileFullPath = fileName;
                return result;
            }
            var userPassParams = string.IsNullOrWhiteSpace(Youtube_User) ? "" : $"-u '{Youtube_User}' -p '{Youtube_Pass}'";
            var cmd = $"youtube-dl -f 'bestaudio' --no-playlist {userPassParams} -o '{fileName}' {vid}";
            var shellResult = ShellHelper.Bash(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl audio exited with code {shellResult.ExitCode}.\n{shellResult.Exception}\n{shellResult.Output}");
            }
            if (!File.Exists(fileName))
            {
                throw new Exception($"Video filename {fileName} not found after youtube-dl");
            }
            result.AudioFileFullPath = fileName;
            return result;
        }

        public static YoutubeVideoResponse DownloadVideo(string vid)
        {
            var result = new YoutubeVideoResponse();
            var fileName = GetVideoFilename(vid);
            if (File.Exists(fileName))
            {
                result.VideoFileFullPath = fileName;
                return result;
            }
            var userPassParams = string.IsNullOrWhiteSpace(Youtube_User) ? "" : $"-u '{Youtube_User}' -p '{Youtube_Pass}'";
            var cmd = $"youtube-dl -f 'bestvideo[height<=720][ext=mp4]' --max-filesize 50M {userPassParams} -o '{fileName}' {vid}";
            
            var shellResult = ShellHelper.Bash(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl video exited with code {shellResult.ExitCode}.\n{shellResult.Exception}\n{shellResult.Output}");
            }
            if (!File.Exists(fileName))
            {
                throw new Exception($"Audio filename {fileName} not found after youtube-dl");
            }
            result.VideoFileFullPath = fileName;
            return result;
        }

        public static bool AudioExists(string vid)
        {
            var fileName = GetAudioFilename(vid);
            return File.Exists(fileName);
        }

        private static string GetAudioFilename(string vid)
        {
            return $"/output/download.audio/{vid}";
        }

        private static string GetVideoFilename(string vid)
        {
            return $"/output/download.audio/{vid}.mp4";
        }
    }
}
