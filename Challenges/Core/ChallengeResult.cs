namespace duo_code.Challenges.Core;

public class ChallengeResult
{
    public bool Success { get; set; }
    public int Score { get; set; } // 0-100
    public string Feedback { get; set; } = "";
    public List<string> PassedTests { get; set; } = new();
    public List<string> FailedTests { get; set; } = new();
}