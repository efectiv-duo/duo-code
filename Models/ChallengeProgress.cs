using System.Text.Json;

namespace duo_code.Models;

public class ChallengeProgress
{
    public Dictionary<int, ChallengeAttempt> Attempts { get; set; } = new();
    public int TotalScore { get; set; }
    public int ChallengesCompleted { get; set; }
    public int ChallengesFailed { get; set; }
    public DateTime LastAttempt { get; set; }
    public TimeSpan TotalTimeSpent { get; set; }
    
    public double CompletionRate => 
        (ChallengesCompleted + ChallengesFailed) > 0 
            ? (double)ChallengesCompleted / (ChallengesCompleted + ChallengesFailed) * 100 
            : 0;
    
    public void RecordAttempt(int challengeId, bool success, int score, TimeSpan timeTaken)
    {
        if (!Attempts.ContainsKey(challengeId))
        {
            Attempts[challengeId] = new ChallengeAttempt { ChallengeId = challengeId };
        }
        
        var attempt = Attempts[challengeId];
        attempt.Attempts++;
        attempt.LastAttemptDate = DateTime.UtcNow;
        
        if (success && !attempt.Completed)
        {
            attempt.Completed = true;
            attempt.CompletedDate = DateTime.UtcNow;
            ChallengesCompleted++;
        }
        else if (!success && attempt.Attempts == 1)
        {
            ChallengesFailed++;
        }
        
        if (score > attempt.BestScore)
        {
            attempt.BestScore = score;
            attempt.BestTime = timeTaken;
        }
        
        TotalScore = Attempts.Values.Sum(a => a.BestScore);
        LastAttempt = DateTime.UtcNow;
        TotalTimeSpent += timeTaken;
    }
    
    public static async Task<ChallengeProgress> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ChallengeProgress();
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<ChallengeProgress>(json) ?? new ChallengeProgress();
        }
        catch
        {
            return new ChallengeProgress();
        }
    }
    
    public async Task SaveAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        await File.WriteAllTextAsync(filePath, json);
    }
}

public class ChallengeAttempt
{
    public int ChallengeId { get; set; }
    public bool Completed { get; set; }
    public int BestScore { get; set; }
    public int Attempts { get; set; }
    public TimeSpan BestTime { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime LastAttemptDate { get; set; }
}