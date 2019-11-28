using System.Diagnostics;
using System.Text;
using System.IO;
using System;

namespace SpleeterAPI
{
    public static class ShellHelper
    {
        public static ShellExecutionResult Execute(string cmd, Action<string> stdErrDataReceivedCallback = null, Action<string> stdOutDataReceivedCallback = null)
        {
            bool isWindows = Startup.IsWindows;
            Startup.EphemeralLog($"Will execute: {cmd}", true);
            var escapedArgs = isWindows ? cmd : cmd.Replace("\"", "\\\"");
            var outputBuilder = new StringBuilder();
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/bash",
                    Arguments = isWindows ? $"/C \"{escapedArgs}\"" : $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };
            process.ErrorDataReceived += new DataReceivedEventHandler
            (
                delegate (object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        stdErrDataReceivedCallback?.Invoke(e.Data);
                    }
                    if (stdErrDataReceivedCallback == null)
                    {
                        Startup.EphemeralLog(e.Data, false);
                    }
                    outputBuilder.AppendLine(e.Data);
                }
            );
            process.OutputDataReceived += new DataReceivedEventHandler
            (
                delegate (object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        stdOutDataReceivedCallback?.Invoke(e.Data);
                    }
                    if (stdOutDataReceivedCallback == null)
                    {
                        Startup.EphemeralLog(e.Data, false);
                    }
                    outputBuilder.AppendLine(e.Data);
                }
            );

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            process.CancelOutputRead();
            process.CancelErrorRead();

            return new ShellExecutionResult()
            {
                ExitCode = process.ExitCode,
                Output = outputBuilder.ToString()
            };
        }

        public static string SanitizeFilename(string filename)
        {
            filename = filename.Replace("/", "").Replace("\\", "").Replace("\"", "");
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

    }
}