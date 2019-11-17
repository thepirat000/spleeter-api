using System;
using System.Diagnostics;
using System.Text;
using System.IO;

namespace SpleeterAPI
{
    public static class ShellHelper
    {
        public static ShellExecutionResult Bash(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");
            var outputBuilder = new StringBuilder();
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
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
                    Console.WriteLine(e.Data);
                    outputBuilder.AppendLine(e.Data);
                }
            );
            process.OutputDataReceived += new DataReceivedEventHandler
            (
                delegate (object sender, DataReceivedEventArgs e)
                {
                    Console.WriteLine(e.Data);
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


    }
}