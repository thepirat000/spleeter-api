using System.Collections.Generic;

namespace SpleeterAPI.Youtube
{
    public class YoutubeProcessRequest
    {
        public string Vid { get; set; }
        public string BaseFormat { get; set; }
        public List<string> SubFormats { get; set; }
        public string Extension { get; set; }
        public YoutubeProcessOptions Options { get; set; }

    }
}
