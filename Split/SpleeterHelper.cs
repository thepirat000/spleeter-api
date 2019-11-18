using Microsoft.Extensions.Logging;

namespace SpleeterAPI.Youtube
{
    public static class SpliterHelper
    {
        public static ShellExecutionResult Split(string inputFile, string fileId, string format, bool includeHighFreq, ILogger log)
        {
            if (format == "karaoke")
            {
                format = "2stems";
            }
            string cmd;
            if (includeHighFreq)
            {
                cmd = $"python -m spleeter separate -i '{inputFile}' -o '/output/{fileId}' -p alt-config/{format}/base_config_hf.json -c mp3";
            }
            else
            {
                cmd = $"python -m spleeter separate -i '{inputFile}' -o '/output/{fileId}' -p spleeter:{format} -c mp3";
            }
            log.LogInformation($"Will execute: {cmd}");
            var result = ShellHelper.Bash(cmd);
            return result;
        }
    }
}
