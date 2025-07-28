using System.Diagnostics;

namespace duo_code.Challenges.Core;

public abstract class ChallengeBase : IChallenge
{
    public abstract int Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract int DifficultyLevel { get; }
    public virtual int MaxScore => 100; // Default max score
    
    public abstract ChallengeSetup Setup { get; }
    public abstract ChallengeVerification Verification { get; }
    
    public virtual async Task<ChallengeResult> VerifyAsync(string workingDirectory, List<string> toolsUsed)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ChallengeResult
        {
            ChallengeId = Id,
            MaxScore = MaxScore,
            ToolsUsed = toolsUsed.Count,
            ToolNames = toolsUsed
        };
        
        try
        {
            // Run file verifications
            var fileResults = await VerifyFiles(workingDirectory);
            result.TestResults.AddRange(fileResults);
            
            // Run test cases
            if (Verification.TestCases.Any())
            {
                var testResults = await RunTestCases(workingDirectory);
                result.TestResults.AddRange(testResults);
            }
            
            // Run custom verification if provided
            if (Verification.CustomVerification != null)
            {
                var customResult = await Verification.CustomVerification(workingDirectory);
                result.Success = customResult.Success;
                result.Score = customResult.Score;
                result.Feedback = customResult.DetailedFeedback;
                
                // Add custom test results
                foreach (var passed in customResult.PassedTests)
                {
                    result.TestResults.Add(new TestCaseResult
                    {
                        TestName = passed,
                        Passed = true,
                        PointsEarned = MaxScore / (customResult.PassedTests.Count + customResult.FailedTests.Count)
                    });
                }
                
                foreach (var failed in customResult.FailedTests)
                {
                    result.TestResults.Add(new TestCaseResult
                    {
                        TestName = failed,
                        Passed = false,
                        PointsEarned = 0
                    });
                }
            }
            else
            {
                // Calculate score from test results
                result.Score = result.TestResults.Sum(t => t.PointsEarned);
                result.Success = result.TestResults.All(t => t.Passed);
                
                if (!result.Success)
                {
                    result.Feedback = GenerateFeedback(result.TestResults);
                }
                else
                {
                    result.Feedback = "Perfect! All tests passed.";
                }
            }
            
            // Add hints if failed
            if (!result.Success && result.Percentage < 50)
            {
                result.Hints = GenerateHints();
            }
            
            // Collect metrics
            result.Metrics = await CollectMetrics(workingDirectory);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Score = 0;
            result.Feedback = $"Error during verification: {ex.Message}";
        }
        
        stopwatch.Stop();
        result.TimeTaken = stopwatch.Elapsed;
        result.Metrics.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        
        return result;
    }
    
    private async Task<List<TestCaseResult>> VerifyFiles(string workingDirectory)
    {
        var results = new List<TestCaseResult>();
        
        foreach (var fileCheck in Verification.FileChecks)
        {
            var filePath = Path.Combine(workingDirectory, fileCheck.Path);
            var testResult = new TestCaseResult
            {
                TestName = $"File: {fileCheck.Path}",
                Expected = fileCheck.ExpectedContent ?? fileCheck.PatternMatch ?? "File exists",
                PointsEarned = 0
            };
            
            if (!File.Exists(filePath))
            {
                if (fileCheck.MustExist)
                {
                    testResult.Passed = false;
                    testResult.Actual = "File not found";
                    testResult.ErrorMessage = $"Required file '{fileCheck.Path}' was not created";
                }
                else
                {
                    testResult.Passed = true;
                    testResult.PointsEarned = MaxScore / Math.Max(Verification.FileChecks.Count, 1);
                }
            }
            else
            {
                var content = await File.ReadAllTextAsync(filePath);
                testResult.Actual = content.Length > 100 ? content.Substring(0, 100) + "..." : content;
                
                if (fileCheck.ExpectedContent != null)
                {
                    testResult.Passed = fileCheck.ExactMatch 
                        ? content.Trim() == fileCheck.ExpectedContent.Trim()
                        : content.Contains(fileCheck.ExpectedContent);
                }
                else if (fileCheck.PatternMatch != null)
                {
                    testResult.Passed = System.Text.RegularExpressions.Regex.IsMatch(content, fileCheck.PatternMatch);
                }
                else
                {
                    testResult.Passed = true;
                }
                
                if (testResult.Passed)
                {
                    testResult.PointsEarned = MaxScore / Math.Max(Verification.FileChecks.Count, 1);
                }
            }
            
            results.Add(testResult);
        }
        
        return results;
    }
    
    private Task<List<TestCaseResult>> RunTestCases(string workingDirectory)
    {
        var results = new List<TestCaseResult>();
        
        foreach (var testCase in Verification.TestCases)
        {
            var testResult = new TestCaseResult
            {
                TestName = testCase.Name,
                Expected = testCase.ExpectedOutput
            };
            
            try
            {
                // This is where you'd run the actual test
                // For now, this is a placeholder
                testResult.Passed = false;
                testResult.Actual = "Test execution not implemented";
                testResult.PointsEarned = testResult.Passed ? testCase.Points : 0;
            }
            catch (Exception ex)
            {
                testResult.Passed = false;
                testResult.ErrorMessage = ex.Message;
                testResult.PointsEarned = 0;
            }
            
            results.Add(testResult);
        }
        
        return Task.FromResult(results);
    }
    
    private string GenerateFeedback(List<TestCaseResult> testResults)
    {
        var feedback = new List<string>();
        var failedTests = testResults.Where(t => !t.Passed).ToList();
        
        feedback.Add($"Passed {testResults.Count - failedTests.Count}/{testResults.Count} tests");
        
        if (failedTests.Any())
        {
            feedback.Add("\nFailed tests:");
            foreach (var test in failedTests.Take(3))
            {
                feedback.Add($"- {test.TestName}: {test.ErrorMessage ?? "Expected output didn't match"}");
            }
            
            if (failedTests.Count > 3)
            {
                feedback.Add($"... and {failedTests.Count - 3} more");
            }
        }
        
        return string.Join("\n", feedback);
    }
    
    protected virtual List<string> GenerateHints()
    {
        return new List<string>
        {
            "Check the file paths in your solution",
            "Make sure you're reading and writing to the correct files",
            "Review the challenge description carefully"
        };
    }
    
    private Task<ChallengeMetrics> CollectMetrics(string workingDirectory)
    {
        var metrics = new ChallengeMetrics();
        
        // Count files and directories created
        try
        {
            var allFiles = Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories);
            metrics.FilesCreated = allFiles.Length;
            
            var allDirs = Directory.GetDirectories(workingDirectory, "*", SearchOption.AllDirectories);
            metrics.DirectoriesCreated = allDirs.Length;
            
            // Count lines of code in common programming files
            var codeExtensions = new[] { ".cs", ".js", ".py", ".java", ".cpp", ".c", ".h", ".ts", ".go" };
            foreach (var file in allFiles)
            {
                if (codeExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    var lines = File.ReadAllLines(file);
                    metrics.LinesOfCode += lines.Length;
                }
            }
        }
        catch
        {
            // Ignore metrics collection errors
        }
        
        return Task.FromResult(metrics);
    }
}