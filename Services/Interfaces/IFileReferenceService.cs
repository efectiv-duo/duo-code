using duo_code.Models;

namespace duo_code.Services.Interfaces;

public interface IFileReferenceService
{
    /// <summary>
    /// Processes file references in user input and returns separated user and system content
    /// </summary>
    /// <param name="input">User input containing @filename references</param>
    /// <returns>Result containing processed user content and system message content</returns>
    Task<FileReferenceResult> ProcessFileReferencesAsync(string input);
    
    /// <summary>
    /// Validates if a file reference syntax is correct
    /// </summary>
    /// <param name="reference">File reference string (e.g., @file.js:10-20)</param>
    /// <returns>True if syntax is valid</returns>
    bool IsValidFileReference(string reference);
}