namespace duo_code.Challenges.Core;

public class ChallengeResult
{
    public int ChallengeId { get; set; }
    public bool Success { get; set; }
    public int Score { get; set; } // Points earned
    public int MaxScore { get; set; } // Maximum possible points
    public double Percentage => MaxScore > 0 ? (double)Score / MaxScore * 100 : 0;
    
    public string Feedback { get; set; } = "";
    public List<string> Hints { get; set; } = new();
    
    public TimeSpan TimeTaken { get; set; }
    public int ToolsUsed { get; set; }
    public List<string> ToolNames { get; set; } = new();
    
    public List<TestCaseResult> TestResults { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    public ChallengeMetrics Metrics { get; set; } = new();
}

public class TestCaseResult
{
    public string TestName { get; set; } = "";
    public bool Passed { get; set; }
    public string Expected { get; set; } = "";
    public string Actual { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public int PointsEarned { get; set; }
}

public class ChallengeMetrics
{
    public int FilesCreated { get; set; }
    public int FilesModified { get; set; }
    public int FilesDeleted { get; set; }
    public int DirectoriesCreated { get; set; }
    public int LinesOfCode { get; set; }
    public double ExecutionTimeMs { get; set; }
    public int MemoryUsedKb { get; set; }
}