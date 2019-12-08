using System;

namespace SpleeterAPI.Youtube
{
    public class YoutubeOutputLogEntry
    {
        private static char Separator = '|';
        private static char Replacement = '-';
        
        private string errors;
        private string title;

        public string Vid { get; set; }
        public string Config { get; set; }
        public DateTime StartTime { get; set; }
        public string Title { get => title; set => title = value?.Replace(Separator, Replacement); }
        public bool Success { get; set; }
        public string Errors { get => errors; set => errors = value?.Replace(Separator, Replacement); }
        public int TimeToSeparate { get; set; }
        public int TimeToProcess { get; set; }
        public string Cache { get; set; }
        public string IpAddress { get; set; }

        public override string ToString()
        {
            return $"{StartTime:yyyy-MM-dd HH:mm:ss}{Separator}"
                + $"{Vid}{Separator}"
                + $"{Config}{Separator}"
                + $"{Title}{Separator}"
                + $"{Success}{Separator}"
                + $"{TimeToProcess}{Separator}"
                + $"{TimeToSeparate}{Separator}"
                + $"{Cache}{Separator}"
                + $"{IpAddress}{Separator}"
                + $"{Errors}";
        }

        public static string GetHeader()
        {
            return $"StartTime{Separator}"
                + $"Vid{Separator}"
                + $"Config{Separator}"
                + $"Title{Separator}"
                + $"Success{Separator}"
                + $"TimeToProcess{Separator}"
                + $"TimeToSeparate{Separator}"
                + $"Cache{Separator}"
                + $"IpAddress{Separator}"
                + $"Errors";
        }

    }
}
