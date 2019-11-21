using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SpleeterAPI.Youtube
{
    public static class SpliterHelper
    {
        private static string Max_Duration = Startup.Configuration["Spleeter:MaxDuration"];

        public static ShellExecutionResult Split(string inputFile, string fileId, string format, bool includeHighFreq, ILogger log, bool isBatch = false)
        {
            if (format == "karaoke" || format == "vocals")
            {
                format = "2stems";
            }
            string cmd;
            
            string formatParam;
            if (includeHighFreq)
            {
                formatParam = $"-p alt-config/{format}/base_config_hf.json";
            }
            else
            {
                formatParam = $"-p spleeter:{format}";
            }
            var maxDurationParam = Max_Duration == "" ? "" : $"--max_duration {Max_Duration}";
            var inputParam = "-i " + (isBatch ? inputFile : $"\"{inputFile}\"");
            cmd = $"python -m spleeter separate {inputParam} -o \"/output/{fileId}\" {maxDurationParam} {formatParam} -c mp3";
            log.LogInformation($"Will execute: {cmd}");
            var result = ShellHelper.Bash(cmd);
            return result;
        }
    }
}
