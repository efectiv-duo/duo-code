using duo_code.Commands.Core;
using duo_code.Challenges.Core;
using duo_code.Models;
using System.Text;

namespace duo_code.Commands.Actions;

public class ChallengeCommand : ICommand
{
    public string Name => "challenge";
    public string Description => "Manage coding challenges for testing AI agent capabilities";
    public CommandType Type => CommandType.Action;
    
    private static readonly string ProgressFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duo-code",
        "challenge-progress.json"
    );
    
    
    public async Task<CommandResult> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return CommandResult.Error(@"Usage: /challenge [subcommand]
Subcommands:
  list           - List all challenges
  run <id>       - Run a specific challenge
  progress       - Show your progress
  reset-all      - Reset all challenge workspaces
  reset <id>     - Reset a specific challenge workspace
  info <id>      - Show detailed challenge information
  run-all        - Run all challenges automatically and generate score");
        }
        
        var subcommand = args[0].ToLower();
        
        return subcommand switch
        {
            "list" => await ListChallenges(),
            "run" => await RunChallenge(args.Skip(1).ToArray()),
            "progress" => await ShowProgress(),
            "reset-all" => await ResetAllChallenges(),
            "reset" => await ResetChallenge(args.Skip(1).ToArray()),
            "info" => await ShowChallengeInfo(args.Skip(1).ToArray()),
            "run-all" => await RunAllChallenges(),
            _ => CommandResult.Error($"Unknown subcommand: {subcommand}")
        };
    }
    
    private async Task<CommandResult> ListChallenges()
    {
        var output = new StringBuilder();
        output.AppendLine("\n🎯 Available Challenges\n");
        
        var challenges = ChallengeRegistry.GetAllChallenges();
        var progress = await ChallengeProgress.LoadAsync(ProgressFilePath);
        
        foreach (var challenge in challenges)
        {
            var status = "❌";
            var score = "";
            
            if (progress.Attempts.TryGetValue(challenge.Id, out var attempt))
            {
                if (attempt.Completed)
                {
                    status = "✅";
                    score = $" [{attempt.BestScore}/100]";
                }
                else if (attempt.Attempts > 0)
                {
                    status = "🔄";
                    score = $" [{attempt.BestScore}/100]";
                }
            }
            
            output.AppendLine($"{status} #{challenge.Id:D3} - {challenge.Name}{score}");
            output.AppendLine($"   Difficulty: {GetDifficultyBar(challenge.DifficultyLevel)} ({challenge.DifficultyLevel}/100)");
            output.AppendLine();
        }
        
        // Summary
        output.AppendLine("📊 Summary:");
        output.AppendLine($"Total Challenges: {challenges.Count()}");
        output.AppendLine($"Completed: {progress.ChallengesCompleted}");
        output.AppendLine($"Total Score: {progress.TotalScore}");
        output.AppendLine($"Completion Rate: {progress.CompletionRate:F1}%");
        
        return CommandResult.Ok(output.ToString());
    }
    
    private async Task<CommandResult> RunChallenge(string[] args)
    {
        if (args.Length == 0)
        {
            return CommandResult.Error("Please specify a challenge ID");
        }
        
        if (!int.TryParse(args[0], out var challengeId))
        {
            return CommandResult.Error("Invalid challenge ID");
        }
        
        var challenge = ChallengeRegistry.GetChallenge(challengeId);
        if (challenge == null)
        {
            return CommandResult.Error($"Challenge #{challengeId} not found");
        }
        
        // Create challenge workspace
        var workspaceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".duo-code",
            "challenges",
            $"challenge-{challengeId}"
        );
        
        if (Directory.Exists(workspaceDir))
        {
            return CommandResult.Error($"Challenge workspace already exists. Use '/challenge reset {challengeId}' to reset.");
        }
        
        Directory.CreateDirectory(workspaceDir);
        
        // Set up challenge files
        foreach (var (path, content) in challenge.SetupFiles)
        {
            var filePath = Path.Combine(workspaceDir, path);
            var directory = Path.GetDirectoryName(filePath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(filePath, content);
        }
        
        var output = new StringBuilder();
        output.AppendLine($"\n🚀 Running Challenge #{challenge.Id}: {challenge.Name}\n");
        output.AppendLine($"Difficulty: {GetDifficultyBar(challenge.DifficultyLevel)} ({challenge.DifficultyLevel}/100)");
        output.AppendLine("📝 Description:");
        output.AppendLine(challenge.Description);
        output.AppendLine();
        output.AppendLine("🎯 Task:");
        output.AppendLine(challenge.InitialPrompt);
        output.AppendLine();
        output.AppendLine($"📁 Workspace: {workspaceDir}\n");
        
        Console.WriteLine(output.ToString());
        
        // TODO: Refactor to use new AgentService architecture  
        // await Program.RunAgent(workspaceDir, challenge.InitialPrompt);
        await Task.CompletedTask;
        
        // Verify the solution
        var result = await challenge.VerifyAsync(workspaceDir);
        
        // Update progress
        var progress = await ChallengeProgress.LoadAsync(ProgressFilePath);
        progress.RecordAttempt(challenge.Id, result.Success, result.Score, TimeSpan.Zero);
        await progress.SaveAsync(ProgressFilePath);
        
        // Display results
        var resultOutput = new StringBuilder();
        resultOutput.AppendLine("\n🔍 Verification Results:");
        if (result.Success)
        {
            resultOutput.AppendLine($"✅ Challenge PASSED! Score: {result.Score}/100");
        }
        else
        {
            resultOutput.AppendLine($"❌ Challenge FAILED. Score: {result.Score}/100");
        }
        resultOutput.AppendLine(result.Feedback);
        
        return CommandResult.Ok(resultOutput.ToString());
    }
    
    private Task<CommandResult> ResetAllChallenges()
    {
        var challengesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".duo-code",
            "challenges"
        );
        
        if (Directory.Exists(challengesDir))
        {
            Directory.Delete(challengesDir, true);
            return Task.FromResult(CommandResult.Ok("All challenge workspaces have been reset."));
        }
        
        return Task.FromResult(CommandResult.Ok("No challenge workspaces to reset."));
    }
    
    private async Task<CommandResult> ShowProgress()
    {
        var progress = await ChallengeProgress.LoadAsync(ProgressFilePath);
        
        var output = new StringBuilder();
        output.AppendLine("\n📊 Your Challenge Progress\n");
        
        output.AppendLine($"Total Score: {progress.TotalScore}");
        output.AppendLine($"Challenges Completed: {progress.ChallengesCompleted}");
        output.AppendLine($"Challenges Attempted: {progress.Attempts.Count}");
        output.AppendLine($"Completion Rate: {progress.CompletionRate:F1}%");
        output.AppendLine($"Time Spent: {progress.TotalTimeSpent.TotalMinutes:F1} minutes");
        
        if (progress.LastAttempt != default)
        {
            output.AppendLine($"Last Attempt: {progress.LastAttempt:yyyy-MM-dd HH:mm} UTC");
        }
        
        
        // Recent attempts
        if (progress.Attempts.Any())
        {
            output.AppendLine("\n🕐 Recent Attempts:");
            var recentAttempts = progress.Attempts.Values
                .OrderByDescending(a => a.LastAttemptDate)
                .Take(5);
            
            foreach (var attempt in recentAttempts)
            {
                var challenge = ChallengeRegistry.GetChallenge(attempt.ChallengeId);
                if (challenge != null)
                {
                    var status = attempt.Completed ? "✅" : "🔄";
                    output.AppendLine($"  {status} #{challenge.Id:D3} - {challenge.Name} [{attempt.BestScore}/100]");
                }
            }
        }
        
        return CommandResult.Ok(output.ToString());
    }
    
    private Task<CommandResult> ResetChallenge(string[] args)
    {
        if (args.Length == 0)
        {
            return Task.FromResult(CommandResult.Error("Please specify a challenge ID"));
        }
        
        if (!int.TryParse(args[0], out var challengeId))
        {
            return Task.FromResult(CommandResult.Error("Invalid challenge ID"));
        }
        
        var workspaceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".duo-code",
            "challenges",
            $"challenge-{challengeId}"
        );
        
        if (!Directory.Exists(workspaceDir))
        {
            return Task.FromResult(CommandResult.Error($"No workspace found for challenge #{challengeId}"));
        }
        
        Directory.Delete(workspaceDir, true);
        
        
        return Task.FromResult(CommandResult.Ok($"Challenge #{challengeId} workspace has been reset."));
    }
    
    private async Task<CommandResult> ShowChallengeInfo(string[] args)
    {
        if (args.Length == 0)
        {
            return CommandResult.Error("Please specify a challenge ID");
        }
        
        if (!int.TryParse(args[0], out var challengeId))
        {
            return CommandResult.Error("Invalid challenge ID");
        }
        
        var challenge = ChallengeRegistry.GetChallenge(challengeId);
        if (challenge == null)
        {
            return CommandResult.Error($"Challenge #{challengeId} not found");
        }
        
        var output = new StringBuilder();
        output.AppendLine($"\n📋 Challenge #{challenge.Id}: {challenge.Name}\n");
        output.AppendLine($"Difficulty: {GetDifficultyBar(challenge.DifficultyLevel)} ({challenge.DifficultyLevel}/100)");
        output.AppendLine($"Max Score: 100 points");
        output.AppendLine();
        output.AppendLine("📝 Description:");
        output.AppendLine(challenge.Description);
        output.AppendLine();
        output.AppendLine("🎯 Task:");
        output.AppendLine(challenge.InitialPrompt);
        output.AppendLine();
        output.AppendLine("📁 Files Created:");
        foreach (var (path, _) in challenge.SetupFiles)
        {
            output.AppendLine($"  • {path}");
        }
        
        var progress = await ChallengeProgress.LoadAsync(ProgressFilePath);
        if (progress.Attempts.TryGetValue(challengeId, out var attempt))
        {
            output.AppendLine();
            output.AppendLine("📊 Your Progress:");
            output.AppendLine($"  Status: {(attempt.Completed ? "✅ Completed" : "🔄 In Progress")}");
            output.AppendLine($"  Best Score: {attempt.BestScore}/100");
            output.AppendLine($"  Attempts: {attempt.Attempts}");
            if (attempt.Completed && attempt.CompletedDate.HasValue)
            {
                output.AppendLine($"  Completed: {attempt.CompletedDate.Value:yyyy-MM-dd HH:mm} UTC");
            }
        }
        
        return CommandResult.Ok(output.ToString());
    }
    
    private async Task<CommandResult> RunAllChallenges()
    {
        var runner = new ChallengeRunner();
        
        Console.WriteLine("\n🏃 Starting automated challenge run...");
        Console.WriteLine("This will test the AI agent on all challenges.\n");
        
        try
        {
            var results = await runner.RunAllChallengesAsync();
            
            // Save the report
            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".duo-code",
                $"test-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt"
            );
            
            var report = runner.GenerateReport(results);
            await File.WriteAllTextAsync(reportPath, report);
            
            // Update progress
            var progress = await ChallengeProgress.LoadAsync(ProgressFilePath);
            foreach (var (challenge, result) in results)
            {
                progress.RecordAttempt(challenge.Id, result.Success, 
                    result.Score, TimeSpan.Zero);
            }
            await progress.SaveAsync(ProgressFilePath);
            
            // Display report
            Console.WriteLine(report);
            Console.WriteLine($"\n📄 Full report saved to: {reportPath}");
            
            var totalPassed = results.Count(r => r.result.Success);
            var avgScore = results.Any() ? results.Sum(r => r.result.Score) / results.Count : 0;
            
            return CommandResult.Ok($"Challenge run completed. {totalPassed}/{results.Count} passed. Average score: {avgScore}/100");
        }
        catch (Exception ex)
        {
            return CommandResult.Error($"Error during challenge run: {ex.Message}");
        }
    }
    
    private static string GetDifficultyBar(int difficulty)
    {
        var filled = difficulty / 20;
        var empty = 5 - filled;
        return new string('█', filled) + new string('░', empty);
    }
}