using duo_code.Tools.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace duo_code.Tools;

public class RunCommandAction : ToolActionBase
{
    public override string ToolName => "RUN_COMMAND";
       public override bool RequiresConfirmation => true;
    public override string Description => @"Execute a shell command.
Format:
RUN_COMMAND: command_to_run";

    public string Command { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Default timeout for commands (e.g., 60 seconds)
        // This can be made configurable if needed.
        const int timeoutMilliseconds = 60 * 1000;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/bash",
            Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {Command}" : $"-c \"{Command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = baseDirectory
        };

        var output = new StringBuilder();
        output.AppendLine("--- Command Output ---");
        output.AppendLine($"Attempting to run: {processStartInfo.FileName} {processStartInfo.Arguments}");
        output.AppendLine($"Working Directory: {processStartInfo.WorkingDirectory}");

        try
        {
            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            bool exited = process.WaitForExit(timeoutMilliseconds);

            output.AppendLine("STDOUT:");
            output.AppendLine(process.StandardOutput.ReadToEnd().Trim());
            output.AppendLine("STDERR:");
            output.AppendLine(process.StandardError.ReadToEnd().Trim());

            if (exited)
            {
                output.AppendLine($"Exit Code: {process.ExitCode}");
                if (process.ExitCode != 0)
                {
                    output.AppendLine("Command failed with a non-zero exit code.");
                }
            }
            else
            {
                output.AppendLine($"Command timed out after {timeoutMilliseconds / 1000} seconds.");
                process.Kill(); // Terminate the process if it timed out
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            output.AppendLine($"ERROR: Could not start process. {ex.Message}");
            output.AppendLine($"Check if '{processStartInfo.FileName}' is in your PATH or correctly specified.");
        }
        catch (Exception ex)
        {
            output.AppendLine($"AN UNEXPECTED ERROR OCCURRED: {ex.Message}");
            output.AppendLine(ex.StackTrace);
        }
        output.AppendLine("--- End Command Output ---");

        return output.ToString();
    }

    public override string ToString()
    {
        return $"{ToolName}: {Command}";
    }
}