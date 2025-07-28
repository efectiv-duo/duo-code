using System.Diagnostics;
using System.Text;
using duo_code.Models;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class SpawnSubagentAction : ToolActionBase
{
    public override string ToolName => "SPAWN_SUBAGENT";
    public override string Description => @"Spawns a subagent process to execute a specific task. The subagent will run in default mode and execute the given prompt, then return the result.
Format:
SPAWN_SUBAGENT: task_description
prompt_for_subagent";
    
    public string TaskDescription { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Prompt))
        {
            return "Error: No prompt provided for subagent";
        }

        try
        {
            // Get the current executable path
            var currentExe = Environment.ProcessPath ?? "dotnet";
            var currentDir = baseDirectory;

            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = currentExe,
                WorkingDirectory = currentDir,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // If we're running with dotnet, we need to add the dll path
            if (currentExe.EndsWith("dotnet") || currentExe.EndsWith("dotnet.exe"))
            {
                var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "duo-code.dll");
                startInfo.Arguments = $"\"{dllPath}\" --subagent \"{Prompt.Replace("\"", "\\\"")}\"";
            }
            else
            {
                startInfo.Arguments = $"--subagent \"{Prompt.Replace("\"", "\\\"")}\"";
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read the output
            var output = new StringBuilder();
            var error = new StringBuilder();

            // Read output and error streams
            var outputTask = Task.Run(() =>
            {
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    output.AppendLine(line);
                }
            });

            var errorTask = Task.Run(() =>
            {
                string? line;
                while ((line = process.StandardError.ReadLine()) != null)
                {
                    error.AppendLine(line);
                }
            });

            // Wait for process to complete with timeout
            if (!process.WaitForExit(300000)) // 5 minute timeout
            {
                process.Kill();
                return "Error: Subagent process timed out after 5 minutes";
            }

            Task.WaitAll(outputTask, errorTask);

            var result = output.ToString();
            var errorOutput = error.ToString();

            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                result += $"\n\nErrors:\n{errorOutput}";
            }

            return $"Subagent task '{TaskDescription}' completed:\n{result}";
        }
        catch (Exception ex)
        {
            return $"Error spawning subagent: {ex.Message}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {TaskDescription}\n{Prompt}";
    }
}