namespace duo_code.Challenges.Core;

public interface IChallenge
{
    int Id { get; }
    string Name { get; }
    string Description { get; }
    int DifficultyLevel { get; } // 1-100
    
    List<(string Path, string Content)> SetupFiles { get; }
    string InitialPrompt { get; }
    
    Task<ChallengeResult> VerifyAsync(string workingDirectory);
}