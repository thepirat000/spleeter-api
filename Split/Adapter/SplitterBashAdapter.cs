namespace SpleeterAPI.Youtube
{
    // Executes spleeter via bash
    public class SplitterBashAdapter : ISplitterAdapter
    {
        private static string Max_Duration = Startup.Configuration["Spleeter:MaxDuration"];
        public SplitProcessResult Split(string inputFile, string outputFolder, string format, bool includeHighFreq, bool isBatch = false)
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
            var maxDurationParam = Max_Duration == "" ? "" : $"--duration {Max_Duration}";
            var inputParam = "-i " + (isBatch ? inputFile : $"\"{inputFile}\"");
            cmd = $"python -m spleeter separate {inputParam} -o \"{outputFolder}\" {maxDurationParam} {formatParam} -c mp3";
            var result = ShellHelper.Execute(cmd);
            return new SplitProcessResult()
            {
                ErrorCount = 0,
                ExitCode = result.ExitCode,
                Output = result.Output
            };
        }

        // TODO:
        public SplitProcessResult Split(string inputFile, YoutubeProcessRequest request, string outputFolder, bool isBatch = false)
        {
            throw new System.NotImplementedException();
        }
    }


}
