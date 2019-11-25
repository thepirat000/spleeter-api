namespace SpleeterAPI.Youtube
{
    public interface ISplitterAdapter
    {
        SplitProcessResult Split(string inputFile, string outputFolder, string format, bool includeHighFreq, bool isBatch = false);
        SplitProcessResult Split(string inputFile, YoutubeProcessRequest request, string outputFolder, bool isBatch = false);
    }


}
