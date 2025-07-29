using System.Text;
using duo_code.Services;

namespace duo_code.Challenges.Core;

public class ChallengeRunner
{
    private readonly string _workspaceRoot;
    
    public ChallengeRunner()
    {
        _workspaceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".duo-code",
            "challenges"
        );
    }
    
    public async Task<List<(IChallenge challenge, ChallengeResult result)>> RunAllChallengesAsync()
    {
        var results = new List<(IChallenge challenge, ChallengeResult result)>();
        var challenges = ChallengeRegistry.GetAllChallenges().ToList();
        
        Console.WriteLine($"\n🚀 Running {challenges.Count} challenges...\n");
        
        foreach (var challenge in challenges)
        {
            Console.WriteLine($"▶️  Challenge #{challenge.Id}: {challenge.Name}");
            
            var challengeResult = await RunSingleChallengeAsync(challenge);
            results.Add((challenge, challengeResult));
            
            if (challengeResult.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   ✅ PASSED - Score: {challengeResult.Score}/100");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   ❌ FAILED - Score: {challengeResult.Score}/100");
            }
            Console.ResetColor();
            Console.WriteLine();
        }
        
        return results;
    }
    
    public async Task<ChallengeResult> RunSingleChallengeAsync(IChallenge challenge)
    {
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
            
            // Note: AI execution would happen here in actual implementation
            // For now, just verify the initial state
            
            // Verify the solution
            var result = await challenge.VerifyAsync(workspaceDir);
            
            return result;
        }
        catch (Exception ex)
        {
            return new ChallengeResult
            {
                Success = false,
                Score = 0,
                Feedback = $"Error during challenge execution: {ex.Message}"
            };
        }
    }
    
    public string GenerateReport(List<(IChallenge challenge, ChallengeResult result)> results)
    {
        var report = new StringBuilder();
        report.AppendLine("\n📊 CHALLENGE TEST REPORT");
        report.AppendLine("=" + new string('=', 50));
        report.AppendLine($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        
        var totalChallenges = results.Count;
        var challengesPassed = results.Count(r => r.result.Success);
        var totalScore = results.Sum(r => r.result.Score);
        var averageScore = totalChallenges > 0 ? totalScore / totalChallenges : 0;
        
        report.AppendLine($"Total Challenges: {totalChallenges}");
        report.AppendLine($"Passed: {challengesPassed}/{totalChallenges} ({(double)challengesPassed/totalChallenges*100:F1}%)");
        report.AppendLine($"\n🎯 AVERAGE SCORE: {averageScore}/100");
        report.AppendLine("\n" + new string('=', 50));
        
        return report.ToString();
    }
}