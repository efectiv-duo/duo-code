using System.Diagnostics;
using System.Text;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class RunCommandAction : ToolActionBase
{
    public override string ToolName => "RUN_COMMAND";
    public override string Description => @"Execute a shell command.
Format:
RUN_COMMAND: command_to_run";
    
    public string Command { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe", // Or "/bin/bash" on Linux/macOS
            Arguments = $"/c {Command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = baseDirectory
        };

        using var process = new Process { StartInfo = processStartInfo };
        var output = new StringBuilder();
        process.Start();
        output.AppendLine($"STDOUT:\n{process.StandardOutput.ReadToEnd()}");
        output.AppendLine($"STDERR:\n{process.StandardError.ReadToEnd()}");
        process.WaitForExit();
        output.AppendLine($"Exit Code: {process.ExitCode}");

        return output.ToString();
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Command}";
    }
}