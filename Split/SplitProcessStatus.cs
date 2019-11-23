using System.Collections.Generic;
using System.Text;

namespace SpleeterAPI.Youtube
{
    public class SplitProcessStatus
    {
        public int ExitCode { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public int FileWrittenCount { get; set; }
        public string Output { get; set; }
    }
}
