using System;

namespace SpleeterAPI.Youtube
{

    public static class SpliterHelper
    {
        private static Lazy<ISplitterAdapter> _cmdAdapter = new Lazy<ISplitterAdapter>(() => new SplitterCmdAdapter());
        private static Lazy<ISplitterAdapter> _bashAdapter = new Lazy<ISplitterAdapter>(() => new SplitterBashAdapter());

        public static SplitProcessResult Split(string inputFile, string outputFolder, string format, bool includeHighFreq, bool isBatch = false)
        {
            bool isWindows = Startup.IsWindows;
            ISplitterAdapter adapter = isWindows ? _cmdAdapter.Value : _bashAdapter.Value;
            return adapter?.Split(inputFile, outputFolder, format, includeHighFreq, isBatch);
        }

        public static SplitProcessResult Split(string inputFile, string outputFolder, YoutubeProcessRequest request, bool isBatch = false)
        {
            bool isWindows = Startup.IsWindows;
            ISplitterAdapter adapter = isWindows ? _cmdAdapter.Value : _bashAdapter.Value;
            return adapter?.Split(inputFile, request, outputFolder, isBatch);
        }

    }
}
