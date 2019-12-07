using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SpleeterAPI.Youtube
{
    public class SplitterCmdAdapter : ISplitterAdapter
    {
        private static Regex FileWrittenRegex = new Regex(@"INFO:spleeter:File\s.*\swritten");
        private static Regex ErrorRegex = new Regex(@"ERROR:spleeter:.*|tensorflow/.*failed to initialize|FileExistsError:\s");
        private static string Max_Duration = Startup.Configuration["Spleeter:MaxDuration"];
        private static string Anaconda_Activate_Script = Startup.Configuration["Spleeter:AnacondaScript"];

        public SplitProcessResult Split(string inputFile, string outputFolder, string format, bool includeHighFreq, bool isBatch = false)
        {
            var output = new StringBuilder();
            var status = new SplitProcessResult();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"cmd.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            process.ErrorDataReceived += new DataReceivedEventHandler(delegate (object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine("stderr", e.Data, status);
                }
            });

            process.OutputDataReceived += new DataReceivedEventHandler(delegate (object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine("stdout", e.Data, status);
                }
            });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var maxDurationParam = Max_Duration == "" ? "" : $"--duration {Max_Duration}";
            string formatParam;
            if (includeHighFreq)
            {
                formatParam = $"-p alt-config/{format}/base_config_hf.json";
            }
            else
            {
                formatParam = $"-p spleeter:{format}";
            }
            var inputParam = "-i " + (isBatch ? inputFile : $"\"{inputFile}\"");

            var separateCommand = @$"spleeter separate {inputParam} -o ""{outputFolder}"" {maxDurationParam} {formatParam} -c mp3";
            using (var sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(Anaconda_Activate_Script);
                    sw.WriteLine(separateCommand);
                    sw.WriteLine("conda deactivate");
                }
            }

            WaitOrKill(process, 15, separateCommand);
            
            status.ExitCode = process.ExitCode != 0 ? process.ExitCode : status.FileWrittenCount == 0 ? -1 : 0;
            return status;
        }

        private static void ProcessOutputLine(string type, string line, SplitProcessResult status)
        {
            Startup.EphemeralLog($"[spleeter] {type}: {line}", false);
            if (type == "stderr" && ErrorRegex.IsMatch(line))
            {
                status.ErrorCount++;
                status.Errors.Add(line);
            }
            else if (FileWrittenRegex.IsMatch(line))
            {
                status.FileWrittenCount++;
            }
        }

        // V2
        public SplitProcessResult Split(string inputFile, YoutubeProcessRequest request, string outputFolder, bool isBatch = false)
        {
            var output = new StringBuilder();
            var status = new SplitProcessResult();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"cmd.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            process.ErrorDataReceived += new DataReceivedEventHandler(delegate (object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine("stderr", e.Data, status);
                }
            });
            process.OutputDataReceived += new DataReceivedEventHandler(delegate (object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine("stdout", e.Data, status);
                }
            });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var maxDurationParam = Max_Duration == "" ? "" : $"--duration {Max_Duration}";
            string formatParam;
            if (request.Options.IncludeHighFrequencies)
            {
                formatParam = $"-p alt-config/{request.BaseFormat}/base_config_hf.json";
            }
            else
            {
                formatParam = $"-p spleeter:{request.BaseFormat}";
            }
            var inputParam = "-i " + (isBatch ? inputFile : $"\"{inputFile}\"");
            var separateCommand = @$"spleeter separate {inputParam} -o ""{outputFolder}"" {maxDurationParam} {formatParam} -c mp3";
            using (var sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(Anaconda_Activate_Script);
                    sw.WriteLine(separateCommand);
                    sw.WriteLine("conda deactivate");
                }
            }

            WaitOrKill(process, 15, separateCommand);

            status.ExitCode = process.ExitCode != 0 ? process.ExitCode : status.FileWrittenCount == 0 ? -1 : 0;
            return status;
        }


        private void WaitOrKill(Process process, int minutes, string command)
        {
            if (!process.WaitForExit(milliseconds: minutes * 60 * 1000))
            {
                Startup.EphemeralLog($"---------------> PROCESS SPLEETER EXITED AFTER TIMEOUT. Killing process. Command: {command}", true);
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    try { process.Kill(); } finally { }
                }
            }
            process.CancelOutputRead();
            process.CancelErrorRead();
        }

    }


}
