namespace duo_code.Challenges.Core;

public interface IChallenge
{
    int Id { get; }
    string Name { get; }
    string Description { get; }
    int DifficultyLevel { get; } // 1-100
    int MaxScore { get; } // Maximum points for this challenge
    
    ChallengeSetup Setup { get; }
    ChallengeVerification Verification { get; }
    
    Task<ChallengeResult> VerifyAsync(string workingDirectory, List<string> toolsUsed);
}

public class ChallengeSetup
{
    public List<FileSetup> RequiredFiles { get; set; } = new();
    public string InitialPrompt { get; set; } = "";
    public Dictionary<string, string> TestData { get; set; } = new();
    public string? SetupInstructions { get; set; }
}

public class FileSetup
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsDirectory { get; set; } = false;
}

public class ChallengeVerification
{
    public List<FileVerification> FileChecks { get; set; } = new();
    public List<OutputVerification> OutputChecks { get; set; } = new();
    public List<TestCase> TestCases { get; set; } = new();
    public Func<string, Task<VerificationDetails>>? CustomVerification { get; set; }
}

public class FileVerification
{
    public string Path { get; set; } = "";
    public string? ExpectedContent { get; set; }
    public string? PatternMatch { get; set; }
    public bool ExactMatch { get; set; } = true;
    public bool MustExist { get; set; } = true;
}

public class OutputVerification
{
    public string Command { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public bool ExactMatch { get; set; } = true;
}

public class TestCase
{
    public string Name { get; set; } = "";
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public int Points { get; set; } = 10;
}

public class VerificationDetails
{
    public bool Success { get; set; }
    public int Score { get; set; }
    public List<string> PassedTests { get; set; } = new();
    public List<string> FailedTests { get; set; } = new();
    public string DetailedFeedback { get; set; } = "";
}