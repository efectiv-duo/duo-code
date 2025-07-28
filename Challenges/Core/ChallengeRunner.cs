using System.Diagnostics;
using System.Text;
using duo_code.Services;
using duo_code.Models;
using duo_code.Tools.Core;

namespace duo_code.Challenges.Core;

public class ChallengeRunner
{
    private readonly CerebrasApiService _apiService;
    private readonly string _workspaceRoot;
    
    public ChallengeRunner(CerebrasApiService apiService)
    {
        _apiService = apiService;
        _workspaceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".duo-code",
            "challenges"
        );
    }
    
    public async Task<BatchTestResult> RunAllChallengesAsync()
    {
        var result = new BatchTestResult();
        var challenges = ChallengeRegistry.GetAllChallenges().ToList();
        
        Console.WriteLine($"\n🚀 Running {challenges.Count} challenges...\n");
        
        foreach (var challenge in challenges)
        {
            Console.WriteLine($"▶️  Challenge #{challenge.Id}: {challenge.Name}");
            
            var challengeResult = await RunSingleChallengeAsync(challenge);
            result.ChallengeResults.Add(challengeResult);
            
            if (challengeResult.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   ✅ PASSED - Score: {challengeResult.Score}/{challengeResult.MaxScore}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   ❌ FAILED - Score: {challengeResult.Score}/{challengeResult.MaxScore}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }
        
        // Calculate final score
        result.TotalChallenges = challenges.Count;
        result.ChallengesPassed = result.ChallengeResults.Count(r => r.Success);
        result.TotalScore = result.ChallengeResults.Sum(r => r.Score);
        result.MaxPossibleScore = result.ChallengeResults.Sum(r => r.MaxScore);
        result.FinalScore = result.MaxPossibleScore > 0 
            ? (int)Math.Round((double)result.TotalScore / result.MaxPossibleScore * 100)
            : 0;
        
        return result;
    }
    
    private async Task<ChallengeResult> RunSingleChallengeAsync(IChallenge challenge)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Setup challenge workspace
            var workspaceDir = Path.Combine(_workspaceRoot, $"challenge-{challenge.Id}");
            
            // Clean up if exists
            if (Directory.Exists(workspaceDir))
            {
                Directory.Delete(workspaceDir, true);
            }
            
            Directory.CreateDirectory(workspaceDir);
            
            // Create challenge files
            foreach (var file in challenge.Setup.RequiredFiles)
            {
                var filePath = Path.Combine(workspaceDir, file.Path);
                var directory = Path.GetDirectoryName(filePath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                if (!file.IsDirectory)
                {
                    await File.WriteAllTextAsync(filePath, file.Content);
                }
            }
            
            // Run AI on the challenge prompt
            await RunAIOnPromptAsync(challenge.Setup.InitialPrompt, workspaceDir);
            
            // Verify the solution
            var result = await challenge.VerifyAsync(workspaceDir, new List<string>());
            
            stopwatch.Stop();
            result.TimeTaken = stopwatch.Elapsed;
            
            return result;
        }
        catch (Exception ex)
        {
            return new ChallengeResult
            {
                ChallengeId = challenge.Id,
                Success = false,
                Score = 0,
                MaxScore = challenge.MaxScore,
                Feedback = $"Error during challenge execution: {ex.Message}",
                TimeTaken = stopwatch.Elapsed
            };
        }
    }
    
    private async Task RunAIOnPromptAsync(string prompt, string workingDirectory)
    {
        // TODO: Refactor to use new AgentService architecture
        // await Program.RunAgent(workingDirectory, prompt);
        await Task.CompletedTask;
    }    
}

public class BatchTestResult
{
    public List<ChallengeResult> ChallengeResults { get; set; } = new();
    public int TotalChallenges { get; set; }
    public int ChallengesPassed { get; set; }
    public int TotalScore { get; set; }
    public int MaxPossibleScore { get; set; }
    public int FinalScore { get; set; } // 0-100
    public DateTime TestRunDate { get; set; } = DateTime.UtcNow;
    
    public string GenerateReport()
    {
        var report = new StringBuilder();
        report.AppendLine("\n📊 CHALLENGE TEST REPORT");
        report.AppendLine("=" + new string('=', 50));
        report.AppendLine($"Date: {TestRunDate:yyyy-MM-dd HH:mm} UTC");
        report.AppendLine($"Total Challenges: {TotalChallenges}");
        report.AppendLine($"Passed: {ChallengesPassed}/{TotalChallenges} ({(double)ChallengesPassed/TotalChallenges*100:F1}%)");
        report.AppendLine($"Total Score: {TotalScore}/{MaxPossibleScore}");
        report.AppendLine($"\n🎯 FINAL SCORE: {FinalScore}/100");
        report.AppendLine("\n" + new string('=', 50));
        
        return report.ToString();
    }
}