using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SpleeterAPI.Youtube
{

    public static class SpliterHelper
    {
        public static SplitProcessStatus Split(string inputFile, string outputFolder, string format, bool includeHighFreq, bool isBatch = false)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            ISplitterAdapter adapter = isWindows ? (ISplitterAdapter)new SplitterCmdAdapter() : new SplitterBashAdapter();
            return adapter?.Split(inputFile, outputFolder, format, includeHighFreq, isBatch);
        }

    }
}
