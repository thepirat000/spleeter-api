using System;
using System.Collections.Concurrent;
using System.IO;

namespace SpleeterAPI.Youtube
{
    public class YoutubeVideoInfo
    {
        public int DurationSeconds { get; set; }
        public string Duration { get; set; }
        public string Title { get; set; }
    }

    public static class YoutubeHelper
    {
        private const string Max_Size = "100M";
        private static string Youtube_User = Startup.Configuration["Youtube:User"];
        private static string Youtube_Pass = Startup.Configuration["Youtube:Pass"];
        private static ConcurrentDictionary<string, YoutubeVideoInfo> _videoInfoCache = new ConcurrentDictionary<string, YoutubeVideoInfo>();

        public static YoutubeVideoInfo GetVideoInfo(string vid)
        { 
            if (_videoInfoCache.TryGetValue(vid, out YoutubeVideoInfo cachedInfo))
            {
                return cachedInfo;
            }
            var cmd = $"youtube-dl -s --get-title --get-duration {vid}";
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
                Title = dataArray[0],
                Duration = dataArray[1]
            };
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

        public static YoutubeResponse DownloadAudio(string vid, string fileId)
        {
            var fileName = GetFilename(vid);
            var result = new YoutubeResponse() { FileId = fileId };
            if (File.Exists(fileName))
            {
                result.AudioFileFullPath = fileName;
                return result;
            }
            string cmd;
            if (string.IsNullOrWhiteSpace(Youtube_User))
            {
                cmd = $"youtube-dl -f 'bestaudio[filesize<{Max_Size}]' --no-playlist -o '{fileName}' {vid}";
            }
            else
            {
                cmd = $"youtube-dl -f 'bestaudio[filesize<{Max_Size}]' --no-playlist -u {Youtube_User} -p {Youtube_Pass} -o '{fileName}' {vid}";
            }
            var shellResult = ShellHelper.Bash(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl -f exited with code {shellResult.ExitCode}.\n{shellResult.Exception}\n{shellResult.Output}");
            }
            if (!File.Exists(fileName))
            {
                throw new Exception($"Filename {fileName} not found after youtube-dl");
            }
            result.AudioFileFullPath = fileName;
            return result;
        }

        public static bool AudioExists(string vid)
        {
            var fileName = GetFilename(vid);
            return File.Exists(fileName);
        }

        private static string GetFilename(string vid)
        {
            return $"/output/download.audio/{vid}";
        }
    }
}
