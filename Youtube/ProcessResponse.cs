namespace SpleeterAPI.Youtube
{
    public class ProcessResponse
    {
        public string FileId { get; set; }
        public string Error { get; set; }
        public string TotalTime { get; set; }
        public string Speed { get; set; }
        public YoutubeOutputLogEntry LogEntry { get; set; }
    }
}
