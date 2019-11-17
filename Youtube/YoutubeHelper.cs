using System;
using System.IO;

namespace SpleeterAPI.Youtube
{
    public static class YoutubeHelper
    {
        private const string Max_Size = "50M";
        private static string Youtube_User = Startup.Configuration["Youtube:User"];
        private static string Youtube_Pass = Startup.Configuration["Youtube:Pass"];

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
                throw new Exception($"youtube-dl exited with code {shellResult.ExitCode}.\n{shellResult.Exception}\n{shellResult.Output}");
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
            return $"/output/download.audio.{vid}";
        }
    }
}
