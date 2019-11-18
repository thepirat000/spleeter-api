namespace SpleeterAPI.Youtube
{
    public static class SpliterHelper
    {
        public static ShellExecutionResult Split(string inputFile, string fileId, string format, bool includeHighFreq)
        {
            if (format == "karaoke")
            {
                format = "2stems";
            }
            ShellExecutionResult result;
            if (includeHighFreq)
            {
                result = ShellHelper.Bash($"python -m spleeter separate -i {inputFile} -o /output/{fileId} -p alt-config/{format}/base_config_hf.json -c mp3");
            }
            else
            {
                result = ShellHelper.Bash($"python -m spleeter separate -i {inputFile} -o /output/{fileId} -p spleeter:{format} -c mp3");
            }
            return result;
        }
    }
}
