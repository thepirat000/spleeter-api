namespace SpleeterAPI.Youtube
{
    public interface ISplitterAdapter
    {
        SplitProcessStatus Split(string inputFile, string outputFolder, string format, bool includeHighFreq, bool isBatch = false);
    }


}
